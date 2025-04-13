using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Events;
using SpeechRecognition.Core.Recovery;
using Microsoft.Extensions.Logging;
using System.Linq;

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
    /// Состояние очереди обработки
    /// </summary>
    public enum QueueState
    {
        /// <summary>
        /// Очередь активна и обрабатывает фрагменты
        /// </summary>
        Active,
        
        /// <summary>
        /// Очередь приостановлена
        /// </summary>
        Paused,
        
        /// <summary>
        /// Очередь завершена или отменена
        /// </summary>
        Completed
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
    /// Аргументы события изменения состояния очереди
    /// </summary>
    public class QueueStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Идентификатор сессии
        /// </summary>
        public Guid SessionId { get; }
        
        /// <summary>
        /// Состояние очереди
        /// </summary>
        public QueueState State { get; }
        
        /// <summary>
        /// Создает новый экземпляр класса QueueStateChangedEventArgs
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="state">Состояние очереди</param>
        public QueueStateChangedEventArgs(Guid sessionId, QueueState state)
        {
            SessionId = sessionId;
            State = state;
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
        private CancellationTokenSource _sessionCancellationSource;
        private readonly ManualResetEventSlim _pauseEvent = new ManualResetEventSlim(true); // По умолчанию не приостановлено
        private readonly ILogger _logger;
        private RetryPolicy _retryPolicy;

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
        /// Событие изменения состояния очереди
        /// </summary>
        public event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

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
        /// Создает новую сессию распознавания речи
        /// </summary>
        /// <param name="recognizer">Распознаватель речи</param>
        /// <param name="logger">Логгер</param>
        public RecognitionSession(ISpeechRecognizer recognizer, ILogger logger)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fragments = new Dictionary<int, SessionFragment>();
            _processingLock = new SemaphoreSlim(1, 1);
            _fragmentLock = new SemaphoreSlim(1, 1);
            _sessionCancellationSource = new CancellationTokenSource();
            Id = Guid.NewGuid();
            CreationTime = DateTime.Now;
            _nextFragmentId = 1;
            _retryPolicy = RetryPolicyFactory.CreateForSpeechRecognition(logger);
        }

        /// <summary>
        /// Добавляет фрагмент в очередь на обработку
        /// </summary>
        /// <param name="audioData">Аудиоданные</param>
        /// <param name="priority">Приоритет обработки</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <returns>Идентификатор добавленного фрагмента</returns>
        public async Task<int> AddFragmentAsync(byte[] audioData, int priority = 0, string metadata = "")
        {
            ThrowIfDisposed();

            await _fragmentLock.WaitAsync();
            try
            {
                int fragmentId = _nextFragmentId++;
                var fragment = new SessionFragment(fragmentId, audioData, priority, metadata);

                lock (_fragments)
                {
                    _fragments.Add(fragmentId, fragment);
                }

                return fragmentId;
            }
            finally
            {
                _fragmentLock.Release();
            }
        }

        /// <summary>
        /// Получает фрагмент по идентификатору
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>Фрагмент или null, если не найден</returns>
        public SessionFragment GetFragment(int fragmentId)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                return _fragments.TryGetValue(fragmentId, out var fragment) ? fragment : null;
            }
        }

        /// <summary>
        /// Получает все фрагменты с указанным статусом
        /// </summary>
        /// <param name="status">Статус фрагментов (null для всех статусов)</param>
        /// <returns>Список фрагментов</returns>
        public List<SessionFragment> GetFragments(FragmentStatus? status = null)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                var query = _fragments.Values.AsEnumerable();
                if (status.HasValue)
                {
                    query = query.Where(f => f.Status == status.Value);
                }
                return query.ToList();
            }
        }

        /// <summary>
        /// Изменяет приоритет фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <returns>true, если приоритет изменен успешно</returns>
        public bool ChangeFragmentPriority(int fragmentId, int newPriority)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment) && fragment.Status == FragmentStatus.Pending)
                {
                    fragment.Priority = newPriority;
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>true, если фрагмент отменен успешно</returns>
        public bool CancelFragment(int fragmentId)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment) && 
                    (fragment.Status == FragmentStatus.Pending || fragment.Status == FragmentStatus.Processing))
                {
                    var oldStatus = fragment.Status;
                    fragment.Status = FragmentStatus.Canceled;
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Очищает все ожидающие фрагменты
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
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragment.FragmentId, oldStatus, FragmentStatus.Canceled));
                }
            }
        }

        /// <summary>
        /// Приостанавливает обработку очереди
        /// </summary>
        public void PauseProcessing()
        {
            lock (_pauseLock)
            {
                if (!_isPaused)
                {
                    _isPaused = true;
                    _pauseEvent.Reset(); // Сигнализирует о приостановке
                    
                    // Вызываем событие изменения состояния очереди
                    OnQueueStateChanged(new QueueStateChangedEventArgs(Id, QueueState.Paused));
                }
            }
        }
        
        /// <summary>
        /// Возобновляет обработку очереди
        /// </summary>
        public void ResumeProcessing()
        {
            lock (_pauseLock)
            {
                if (_isPaused)
                {
                    _isPaused = false;
                    _pauseEvent.Set(); // Сигнализирует о возобновлении
                    
                    // Вызываем событие изменения состояния очереди
                    OnQueueStateChanged(new QueueStateChangedEventArgs(Id, QueueState.Active));
                }
            }
        }

        /// <summary>
        /// Отменяет все текущие операции и освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _sessionCancellationSource?.Cancel();
            _sessionCancellationSource?.Dispose();
            _processingLock?.Dispose();
            _fragmentLock?.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(RecognitionSession));
        }

        protected virtual void OnFragmentStatusChanged(FragmentStatusChangedEventArgs e)
        {
            FragmentStatusChanged?.Invoke(this, e);
        }

        protected virtual void OnRecognitionStarted(RecognitionEventArgs e)
        {
            RecognitionStarted?.Invoke(this, e);
        }

        protected virtual void OnRecognitionCompleted(RecognitionEventArgs e)
        {
            RecognitionCompleted?.Invoke(this, e);
        }

        protected virtual void OnQueueStateChanged(QueueStateChangedEventArgs e)
        {
            QueueStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Асинхронно обрабатывает фрагмент аудио
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>Результат распознавания или null, если фрагмент не найден или отменен</returns>
        public Task<RecognitionResult> ProcessFragmentAsync(int fragmentId)
        {
            return ProcessFragmentAsync(fragmentId, CancellationToken.None);
        }

        /// <summary>
        /// Асинхронно обрабатывает фрагмент аудио
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания или null, если фрагмент не найден или отменен</returns>
        public async Task<RecognitionResult> ProcessFragmentAsync(int fragmentId, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            // Проверка и подготовка фрагмента
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

                var oldStatus = fragment.Status;
                fragment.Status = FragmentStatus.Processing;
                OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Processing));
            }

            int attemptCount = 0;
            const int maxAttempts = 3;

            while (attemptCount < maxAttempts && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Ожидаем доступ к распознавателю
                    await _processingLock.WaitAsync(cancellationToken);

                    try
                    {
                        // Используем политику восстановления для распознавания
                        return await _retryPolicy.ExecuteAsync(async (token) =>
                        {
                            var startTime = DateTime.Now;
                            var recognizedText = await _recognizer.RecognizeSpeechAsync(fragment.AudioData, token);
                            var endTime = DateTime.Now;

                            var result = new RecognitionResult(Id, fragmentId, recognizedText, startTime, endTime);

                            lock (_fragments)
                            {
                                if (_fragments.TryGetValue(fragmentId, out var updatedFragment))
                                {
                                    var oldStatus = updatedFragment.Status;
                                    updatedFragment.Status = FragmentStatus.Completed;
                                    updatedFragment.Result = result;
                                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Completed));
                                }
                            }

                            return result;
                        }, $"Распознавание фрагмента {fragmentId}", cancellationToken);
                    }
                    finally
                    {
                        _processingLock.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Обработка отменена, устанавливаем статус и выходим
                    lock (_fragments)
                    {
                        if (_fragments.TryGetValue(fragmentId, out fragment))
                        {
                            var oldStatus = fragment.Status;
                            fragment.Status = FragmentStatus.Canceled;
                            OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                        }
                    }
                    throw; // Пробрасываем исключение для корректной обработки вызывающим кодом
                }
                catch (Exception ex)
                {
                    // Обработка ошибки
                    attemptCount++;
                    _logger.LogWarning(ex, $"Ошибка при обработке фрагмента {fragmentId}. Попытка {attemptCount} из {maxAttempts}");

                    // Если исчерпаны все попытки, устанавливаем статус ошибки
                    if (attemptCount >= maxAttempts)
                    {
                        lock (_fragments)
                        {
                            if (_fragments.TryGetValue(fragmentId, out fragment))
                            {
                                var oldStatus = fragment.Status;
                                fragment.Status = FragmentStatus.Failed;
                                OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Failed));
                            }
                        }
                        _logger.LogError(ex, $"Фрагмент {fragmentId} не может быть обработан после {maxAttempts} попыток");
                        return null;
                    }

                    // Добавляем задержку перед следующей попыткой
                    int delayMs = 500 * (int)Math.Pow(2, attemptCount - 1); // Экспоненциальная задержка
                    _logger.LogInformation($"Повторная попытка через {delayMs} мс...");
                    
                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Задержка отменена, выходим из цикла
                        lock (_fragments)
                        {
                            if (_fragments.TryGetValue(fragmentId, out fragment))
                            {
                                var oldStatus = fragment.Status;
                                fragment.Status = FragmentStatus.Canceled;
                                OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                            }
                        }
                        throw;
                    }
                }
            }

            // Этот код выполняется только если cancellationToken.IsCancellationRequested == true
            // и исключение не было выброшено в блоке catch
            if (cancellationToken.IsCancellationRequested)
            {
                lock (_fragments)
                {
                    if (_fragments.TryGetValue(fragmentId, out fragment))
                    {
                        var oldStatus = fragment.Status;
                        fragment.Status = FragmentStatus.Canceled;
                        OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Canceled));
                    }
                }
                throw new OperationCanceledException(cancellationToken);
            }

            return null;
        }

        /// <summary>
        /// Отменяет все фрагменты в сессии
        /// </summary>
        public void CancelAllFragments()
        {
            ThrowIfDisposed();

            // Отменяем текущий токен отмены для сессии
            var oldCts = Interlocked.Exchange(ref _sessionCancellationSource, new CancellationTokenSource());
            try
            {
                oldCts?.Cancel();
                oldCts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Игнорируем, если токен уже был освобожден
            }

            lock (_fragments)
            {
                foreach (var fragment in _fragments.Values.Where(f => 
                    f.Status == FragmentStatus.Pending || f.Status == FragmentStatus.Processing))
                {
                    var oldStatus = fragment.Status;
                    fragment.Status = FragmentStatus.Canceled;
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragment.FragmentId, oldStatus, FragmentStatus.Canceled));
                }
            }
        }

        /// <summary>
        /// Запускает асинхронную обработку фрагментов с учетом приоритета
        /// </summary>
        /// <param name="maxParallelProcessing">Максимальное количество одновременно обрабатываемых фрагментов</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public Task ProcessQueueAsync(int maxParallelProcessing = 1)
        {
            return ProcessQueueAsync(maxParallelProcessing, CancellationToken.None);
        }

        /// <summary>
        /// Запускает асинхронную обработку фрагментов с учетом приоритета
        /// </summary>
        /// <param name="maxParallelProcessing">Максимальное количество одновременно обрабатываемых фрагментов</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task ProcessQueueAsync(int maxParallelProcessing, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _sessionCancellationSource.Token);
            var combinedToken = linkedCts.Token;

            using var parallelLock = new SemaphoreSlim(maxParallelProcessing, maxParallelProcessing);
            var processingTasks = new List<Task>();

            try
            {
                while (!combinedToken.IsCancellationRequested)
                {
                    // Проверяем состояние паузы перед обработкой фрагментов
                    bool isPaused;
                    lock (_pauseLock)
                    {
                        isPaused = _isPaused;
                    }

                    if (isPaused)
                    {
                        // Ожидаем сигнала о возобновлении
                        if (!_pauseEvent.Wait(100, combinedToken))
                        {
                            // Если пауза все еще активна, переходим к следующей итерации
                            continue;
                        }
                    }

                    var pendingFragments = GetPendingFragments();
                    if (!pendingFragments.Any())
                    {
                        await Task.Delay(100, combinedToken);
                        continue;
                    }

                    foreach (var fragment in pendingFragments)
                    {
                        await parallelLock.WaitAsync(combinedToken);

                        var processingTask = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessFragmentAsync(fragment.FragmentId, combinedToken);
                            }
                            finally
                            {
                                parallelLock.Release();
                            }
                        }, combinedToken);

                        processingTasks.Add(processingTask);
                        processingTasks.RemoveAll(t => t.IsCompleted);
                    }
                }

                // Если дошли сюда и токен отменен, пробрасываем исключение
                combinedToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Ошибка при обработке очереди фрагментов", ex);
            }
        }

        private List<SessionFragment> GetPendingFragments()
        {
            lock (_fragments)
            {
                return _fragments.Values
                    .Where(f => f.Status == FragmentStatus.Pending)
                    .OrderBy(f => f.Priority)
                    .ThenBy(f => f.AddedTime)
                    .ToList();
            }
        }

        /// <summary>
        /// Повторно обрабатывает фрагмент, завершившийся с ошибкой
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>True если фрагмент поставлен на повторную обработку, иначе false</returns>
        public bool RetryFailedFragment(int fragmentId)
        {
            return RetryFailedFragment(fragmentId, CancellationToken.None);
        }

        /// <summary>
        /// Повторно обрабатывает фрагмент, завершившийся с ошибкой
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>True если фрагмент поставлен на повторную обработку, иначе false</returns>
        public bool RetryFailedFragment(int fragmentId, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            lock (_fragments)
            {
                if (_fragments.TryGetValue(fragmentId, out var fragment) && fragment.Status == FragmentStatus.Failed)
                {
                    var oldStatus = fragment.Status;
                    fragment.Status = FragmentStatus.Pending;
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragmentId, oldStatus, FragmentStatus.Pending));
                    
                    // Запускаем обработку асинхронно
                    _ = Task.Run(async () => 
                    {
                        try 
                        {
                            await ProcessFragmentAsync(fragmentId, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, $"Ошибка при повторной обработке фрагмента {fragmentId}");
                        }
                    }, cancellationToken);
                    
                    return true;
                }
                
                return false;
            }
        }

        /// <summary>
        /// Повторно обрабатывает все фрагменты, завершившиеся с ошибкой
        /// </summary>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Количество фрагментов, поставленных на повторную обработку</returns>
        public int RetryAllFailedFragments(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            int retryCount = 0;
            
            lock (_fragments)
            {
                foreach (var fragment in _fragments.Values.Where(f => f.Status == FragmentStatus.Failed).ToList())
                {
                    var oldStatus = fragment.Status;
                    fragment.Status = FragmentStatus.Pending;
                    OnFragmentStatusChanged(new FragmentStatusChangedEventArgs(Id, fragment.FragmentId, oldStatus, FragmentStatus.Pending));
                    retryCount++;
                }
            }
            
            // Если есть фрагменты для повторной обработки, запускаем обработку очереди
            if (retryCount > 0)
            {
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await ProcessQueueAsync(1, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Ошибка при обработке очереди повторных попыток");
                    }
                }, cancellationToken);
            }
            
            return retryCount;
        }

        /// <summary>
        /// Получает количество фрагментов с определенным статусом
        /// </summary>
        /// <param name="status">Статус (null для подсчета всех фрагментов)</param>
        /// <returns>Количество фрагментов</returns>
        public int GetFragmentsCount(FragmentStatus? status = null)
        {
            ThrowIfDisposed();
            
            lock (_fragments)
            {
                if (status.HasValue)
                {
                    return _fragments.Values.Count(f => f.Status == status.Value);
                }
                else
                {
                    return _fragments.Count;
                }
            }
        }
    }
} 