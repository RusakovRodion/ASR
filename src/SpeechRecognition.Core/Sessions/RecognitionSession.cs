using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Events;

namespace SpeechRecognition.Core.Sessions
{
    /// <summary>
    /// Статус фрагмента в сессии распознавания
    /// </summary>
    public enum FragmentStatus
    {
        /// <summary>
        /// Ожидает обработки
        /// </summary>
        Pending,

        /// <summary>
        /// Находится в процессе обработки
        /// </summary>
        Processing,

        /// <summary>
        /// Обработка завершена успешно
        /// </summary>
        Completed,

        /// <summary>
        /// Обработка завершена с ошибкой
        /// </summary>
        Failed,

        /// <summary>
        /// Обработка отменена
        /// </summary>
        Canceled
    }

    /// <summary>
    /// Представляет фрагмент в сессии распознавания
    /// </summary>
    public class SessionFragment
    {
        /// <summary>
        /// Идентификатор фрагмента
        /// </summary>
        public int FragmentId { get; }

        /// <summary>
        /// Аудиоданные фрагмента
        /// </summary>
        public byte[] AudioData { get; }

        /// <summary>
        /// Приоритет обработки (меньшее значение - более высокий приоритет)
        /// </summary>
        public int Priority { get; set; }

        /// <summary>
        /// Время добавления фрагмента
        /// </summary>
        public DateTime AddedTime { get; }

        /// <summary>
        /// Статус фрагмента
        /// </summary>
        public FragmentStatus Status { get; set; }

        /// <summary>
        /// Результат распознавания (null, если еще не обработан)
        /// </summary>
        public RecognitionResult Result { get; set; }

        /// <summary>
        /// Пользовательские метаданные
        /// </summary>
        public string Metadata { get; set; }

        /// <summary>
        /// Создает новый фрагмент сессии
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="audioData">Аудиоданные</param>
        /// <param name="priority">Приоритет обработки</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        public SessionFragment(int fragmentId, byte[] audioData, int priority = 0, string metadata = "")
        {
            if (audioData == null || audioData.Length == 0)
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioData));

            FragmentId = fragmentId;
            AudioData = audioData;
            Priority = priority;
            AddedTime = DateTime.Now;
            Status = FragmentStatus.Pending;
            Result = null;
            Metadata = metadata ?? string.Empty;
        }
    }

    /// <summary>
    /// Аргументы события изменения статуса фрагмента
    /// </summary>
    public class FragmentStatusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Идентификатор сессии
        /// </summary>
        public Guid SessionId { get; }

        /// <summary>
        /// Идентификатор фрагмента
        /// </summary>
        public int FragmentId { get; }

        /// <summary>
        /// Предыдущий статус
        /// </summary>
        public FragmentStatus OldStatus { get; }

        /// <summary>
        /// Новый статус
        /// </summary>
        public FragmentStatus NewStatus { get; }

        /// <summary>
        /// Создает аргументы события изменения статуса фрагмента
        /// </summary>
        public FragmentStatusChangedEventArgs(Guid sessionId, int fragmentId, FragmentStatus oldStatus, FragmentStatus newStatus)
        {
            SessionId = sessionId;
            FragmentId = fragmentId;
            OldStatus = oldStatus;
            NewStatus = newStatus;
        }
    }

    /// <summary>
    /// Представляет сессию распознавания речи
    /// </summary>
    public class RecognitionSession : IDisposable
    {
        private readonly ISpeechRecognizer _recognizer;
        private readonly Dictionary<int, SessionFragment> _fragments;
        private readonly SemaphoreSlim _processingLock;
        private readonly SemaphoreSlim _fragmentLock;
        private bool _isDisposed;
        private bool _isPaused;
        private readonly object _pauseLock = new object();
        private int _nextFragmentId;

        /// <summary>
        /// Событие, возникающее при получении результата распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionCompleted;

        /// <summary>
        /// Событие, возникающее перед началом распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionStarted;

        /// <summary>
        /// Событие, возникающее при изменении статуса фрагмента
        /// </summary>
        public event EventHandler<FragmentStatusChangedEventArgs> FragmentStatusChanged;

        /// <summary>
        /// Идентификатор сессии
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Время создания сессии
        /// </summary>
        public DateTime CreationTime { get; }

        /// <summary>
        /// Количество обработанных фрагментов
        /// </summary>
        public int ProcessedFragmentsCount
        {
            get
            {
                lock (_fragments)
                {
                    return _fragments.Values.Count(f => f.Status == FragmentStatus.Completed);
                }
            }
        }

        /// <summary>
        /// Количество ожидающих фрагментов
        /// </summary>
        public int PendingFragmentsCount
        {
            get
            {
                lock (_fragments)
                {
                    return _fragments.Values.Count(f => f.Status == FragmentStatus.Pending);
                }
            }
        }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionSession
        /// </summary>
        /// <param name="recognizer">Распознаватель речи</param>
        public RecognitionSession(ISpeechRecognizer recognizer)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _fragments = new Dictionary<int, SessionFragment>();
            _processingLock = new SemaphoreSlim(1, 1);
            _fragmentLock = new SemaphoreSlim(1, 1);
            _isDisposed = false;
            _isPaused = false;
            _nextFragmentId = 0;
            
            Id = Guid.NewGuid();
            CreationTime = DateTime.Now;
        }

        /// <summary>
        /// Добавляет фрагмент в сессию с указанным приоритетом
        /// </summary>
        /// <param name="audioData">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет обработки (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <returns>Идентификатор фрагмента</returns>
        public async Task<int> AddFragmentAsync(byte[] audioData, int priority = 0, string metadata = "")
        {
            ThrowIfDisposed();

            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioData));
            }

            await _fragmentLock.WaitAsync();
            try
            {
                int fragmentId = _nextFragmentId++;
                var fragment = new SessionFragment(fragmentId, audioData, priority, metadata);
                
                lock (_fragments)
                {
                    _fragments.Add(fragmentId, fragment);
                }
                
                if (!_isPaused)
                {
                    // Запускаем обработку фрагмента, если сессия не приостановлена
                    _ = ProcessFragmentInternalAsync(fragmentId, CancellationToken.None);
                }
                
                return fragmentId;
            }
            finally
            {
                _fragmentLock.Release();
            }
        }

        /// <summary>
        /// Изменяет приоритет фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <returns>true, если приоритет успешно изменен</returns>
        public bool ChangeFragmentPriority(int fragmentId, int newPriority)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment))
                {
                    if (fragment.Status == FragmentStatus.Pending)
                    {
                        fragment.Priority = newPriority;
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>true, если обработка успешно отменена</returns>
        public bool CancelFragment(int fragmentId)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment))
                {
                    if (fragment.Status == FragmentStatus.Pending)
                    {
                        var oldStatus = fragment.Status;
                        fragment.Status = FragmentStatus.Canceled;
                        
                        OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                            Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                        
                        return true;
                    }
                }
            }
            
            return false;
        }

        /// <summary>
        /// Приостанавливает обработку фрагментов в сессии
        /// </summary>
        public void Pause()
        {
            ThrowIfDisposed();
            
            lock (_pauseLock)
            {
                if (!_isPaused)
                {
                    _isPaused = true;
                }
            }
        }

        /// <summary>
        /// Возобновляет обработку фрагментов в сессии
        /// </summary>
        public void Resume()
        {
            ThrowIfDisposed();
            
            lock (_pauseLock)
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    
                    // Запускаем обработку всех ожидающих фрагментов
                    lock (_fragments)
                    {
                        var pendingFragments = _fragments.Values
                            .Where(f => f.Status == FragmentStatus.Pending)
                            .OrderBy(f => f.Priority)
                            .ThenBy(f => f.AddedTime)
                            .ToList();
                        
                        foreach (var fragment in pendingFragments)
                        {
                            _ = ProcessFragmentInternalAsync(fragment.FragmentId, CancellationToken.None);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Получает информацию о фрагменте
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>Информация о фрагменте или null, если не найден</returns>
        public SessionFragment GetFragment(int fragmentId)
        {
            ThrowIfDisposed();
            
            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment))
                {
                    return fragment;
                }
            }
            
            return null;
        }

        /// <summary>
        /// Получает список всех фрагментов в сессии
        /// </summary>
        /// <param name="statusFilter">Фильтр по статусу (null для всех фрагментов)</param>
        /// <returns>Список фрагментов</returns>
        public IReadOnlyList<SessionFragment> GetFragments(FragmentStatus? statusFilter = null)
        {
            ThrowIfDisposed();
            
            lock (_fragments)
            {
                // Получаем список фрагментов из словаря
                List<SessionFragment> fragments = _fragments.Values.ToList();
                
                if (statusFilter.HasValue)
                {
                    fragments = fragments.Where(f => f.Status == statusFilter.Value).ToList();
                }
                
                return fragments.OrderBy(f => f.Priority).ThenBy(f => f.FragmentId).ToList();
            }
        }

        /// <summary>
        /// Очищает ожидающие фрагменты из сессии
        /// </summary>
        public void ClearPendingFragments()
        {
            ThrowIfDisposed();
            
            lock (_fragments)
            {
                var pendingFragments = _fragments.Values
                    .Where(f => f.Status == FragmentStatus.Pending)
                    .ToList();
                
                foreach (var fragment in pendingFragments)
                {
                    var oldStatus = fragment.Status;
                    fragment.Status = FragmentStatus.Canceled;
                    
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                        Id, fragment.FragmentId, oldStatus, FragmentStatus.Canceled));
                }
            }
        }

        /// <summary>
        /// Обрабатывает фрагмент аудиоданных в рамках сессии
        /// </summary>
        /// <param name="audioFragment">Фрагмент аудиоданных</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания</returns>
        public async Task<RecognitionResult> ProcessFragmentAsync(byte[] audioFragment, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (audioFragment == null || audioFragment.Length == 0)
            {
                throw new ArgumentException("Аудиофрагмент не может быть пустым", nameof(audioFragment));
            }

            int fragmentId = await AddFragmentAsync(audioFragment);
            
            // Ждем результат обработки фрагмента
            while (true)
            {
                SessionFragment fragment;
                lock (_fragments)
                {
                    if (!_fragments.TryGetValue(fragmentId, out fragment))
                    {
                        return null;
                    }
                    
                    if (fragment.Status == FragmentStatus.Completed)
                    {
                        return fragment.Result;
                    }
                    
                    if (fragment.Status == FragmentStatus.Failed || fragment.Status == FragmentStatus.Canceled)
                    {
                        return null;
                    }
                }
                
                await Task.Delay(100, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async Task ProcessFragmentInternalAsync(int fragmentId, CancellationToken cancellationToken)
        {
            SessionFragment fragment;
            lock (_fragments)
            {
                if (!_fragments.TryGetValue(fragmentId, out fragment) || fragment.Status != FragmentStatus.Pending)
                {
                    return;
                }
                
                // Проверяем, не приостановлена ли сессия
                if (_isPaused)
                {
                    return;
                }
                
                // Изменяем статус на "в обработке"
                var oldStatus = fragment.Status;
                fragment.Status = FragmentStatus.Processing;
                
                OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                    Id, fragmentId, oldStatus, FragmentStatus.Processing));
            }
            
            try
            {
                // Ожидаем доступ к распознавателю
                await _processingLock.WaitAsync(cancellationToken);
                
                var startTime = DateTime.Now;
                
                // Уведомляем о начале распознавания
                var initialResult = new RecognitionResult(Id, fragmentId, string.Empty, startTime, DateTime.MinValue);
                OnRecognitionStarted(new RecognitionEventArgs(initialResult));
                
                // Распознаем аудиоданные
                string recognizedText = await _recognizer.RecognizeSpeechAsync(fragment.AudioData, cancellationToken);
                
                var endTime = DateTime.Now;
                
                // Создаем результат распознавания
                var result = new RecognitionResult(Id, fragmentId, recognizedText, startTime, endTime);
                
                lock (_fragments)
                {
                    if (_fragments.TryGetValue(fragmentId, out var updatedFragment) && updatedFragment.Status == FragmentStatus.Processing)
                    {
                        var oldStatus = updatedFragment.Status;
                        updatedFragment.Status = FragmentStatus.Completed;
                        updatedFragment.Result = result;
                        
                        OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                            Id, fragmentId, oldStatus, FragmentStatus.Completed));
                        
                        // Уведомляем о завершении распознавания
                        OnRecognitionCompleted(new RecognitionEventArgs(result));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                lock (_fragments)
                {
                    if (_fragments.TryGetValue(fragmentId, out var updatedFragment) && updatedFragment.Status == FragmentStatus.Processing)
                    {
                        var oldStatus = updatedFragment.Status;
                        updatedFragment.Status = FragmentStatus.Canceled;
                        
                        OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                            Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                    }
                }
            }
            catch (Exception)
            {
                lock (_fragments)
                {
                    if (_fragments.TryGetValue(fragmentId, out var updatedFragment) && updatedFragment.Status == FragmentStatus.Processing)
                    {
                        var oldStatus = updatedFragment.Status;
                        updatedFragment.Status = FragmentStatus.Failed;
                        
                        OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(
                            Id, fragmentId, oldStatus, FragmentStatus.Failed));
                    }
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        protected virtual void OnRecognitionStarted(RecognitionEventArgs e)
        {
            RecognitionStarted?.Invoke(this, e);
        }

        protected virtual void OnRecognitionCompleted(RecognitionEventArgs e)
        {
            RecognitionCompleted?.Invoke(this, e);
        }

        protected virtual void OnFragmentStatusChanged(FragmentStatusChangedEventArgs e)
        {
            FragmentStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _processingLock.Dispose();
            _fragmentLock.Dispose();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(RecognitionSession));
            }
        }
    }
} 