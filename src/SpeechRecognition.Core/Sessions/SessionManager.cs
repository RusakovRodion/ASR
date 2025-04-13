using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Events;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Recovery;

namespace SpeechRecognition.Core.Sessions
{
    /// <summary>
    /// Менеджер сессий распознавания речи
    /// </summary>
    public class SessionManager : IDisposable, IAsyncDisposable
    {
        private readonly ISpeechRecognizer _recognizer;
        private readonly ConcurrentDictionary<Guid, RecognitionSession> _sessions;
        private bool _isDisposed;
        private RecognitionSession _defaultSession;
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _sessionCancellationTokens;
        private CancellationTokenSource _globalCancellationSource;
        private readonly ILogger _logger;
        private RetryPolicy _retryPolicy;
        private readonly object _sessionsLock = new object();

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
        /// Событие, возникающее при изменении состояния очереди (приостановка/возобновление)
        /// </summary>
        public event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        /// <summary>
        /// Событие, возникающее при создании новой сессии
        /// </summary>
        public event EventHandler<SessionEventArgs> SessionCreated;

        /// <summary>
        /// Создает новый экземпляр класса SessionManager
        /// </summary>
        /// <param name="recognizer">Распознаватель речи, который будет использоваться для всех сессий</param>
        /// <param name="logger">Логгер</param>
        public SessionManager(ISpeechRecognizer recognizer, ILogger logger)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = new ConcurrentDictionary<Guid, RecognitionSession>();
            _sessionCancellationTokens = new ConcurrentDictionary<Guid, CancellationTokenSource>();
            _globalCancellationSource = new CancellationTokenSource();
            _isDisposed = false;
            _retryPolicy = RetryPolicyFactory.CreateDefault(logger);
            
            // Создаем сессию по умолчанию для управления очередью
            _defaultSession = CreateSession();
        }

        /// <summary>
        /// Создает новую сессию распознавания
        /// </summary>
        /// <returns>Созданная сессия</returns>
        public RecognitionSession CreateSession()
        {
            ThrowIfDisposed();
            
            var session = new RecognitionSession(_recognizer, _logger);
            session.FragmentStatusChanged += OnSessionFragmentStatusChanged;
            session.QueueStateChanged += OnSessionQueueStateChanged;

            lock (_sessionsLock)
            {
                _sessions.TryAdd(session.Id, session);
                
                // Если это первая сессия, устанавливаем ее как сессию по умолчанию
                if (_defaultSession == null)
                {
                    _defaultSession = session;
                }
            }

            SessionCreated?.Invoke(this, new SessionEventArgs(session.Id));
            
            return session;
        }

        // Обработчики событий сессии
        private void OnSessionRecognitionStarted(object sender, RecognitionEventArgs e)
        {
            RecognitionStarted?.Invoke(this, e);
        }

        private void OnSessionRecognitionCompleted(object sender, RecognitionEventArgs e)
        {
            RecognitionCompleted?.Invoke(this, e);
        }
        
        private void OnSessionFragmentStatusChanged(object sender, FragmentStatusChangedEventArgs e)
        {
            FragmentStatusChanged?.Invoke(this, e);
        }
        
        private void OnSessionQueueStateChanged(object sender, QueueStateChangedEventArgs e)
        {
            QueueStateChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Получает сессию по идентификатору
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <returns>Сессия или null, если не найдена</returns>
        public RecognitionSession GetSession(Guid sessionId)
        {
            ThrowIfDisposed();

            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>
        /// Удаляет сессию
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <returns>true, если сессия была найдена и удалена; иначе false</returns>
        public bool RemoveSession(Guid sessionId)
        {
            ThrowIfDisposed();
            
            // Не позволяем удалить сессию по умолчанию
            if (sessionId == _defaultSession.Id)
            {
                return false;
            }

            if (_sessions.TryRemove(sessionId, out var session))
            {
                // Отписываемся от событий сессии
                session.RecognitionStarted -= OnSessionRecognitionStarted;
                session.RecognitionCompleted -= OnSessionRecognitionCompleted;
                session.FragmentStatusChanged -= OnSessionFragmentStatusChanged;
                session.QueueStateChanged -= OnSessionQueueStateChanged;
                
                session.Dispose();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Получает все активные сессии
        /// </summary>
        /// <returns>Список активных сессий</returns>
        public IReadOnlyCollection<RecognitionSession> GetAllSessions()
        {
            ThrowIfDisposed();

            return _sessions.Values.Where(s => s.Id != _defaultSession.Id).ToList();
        }

        /// <summary>
        /// Обрабатывает фрагмент аудиоданных в рамках сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="audioFragment">Аудиоданные фрагмента</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания</returns>
        public async Task<RecognitionResult> ProcessFragmentAsync(Guid sessionId, byte[] audioFragment, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (audioFragment == null || audioFragment.Length == 0)
            {
                throw new ArgumentException("Аудиофрагмент не может быть пустым", nameof(audioFragment));
            }

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                // Сначала добавляем фрагмент в сессию
                int fragmentId = await session.AddFragmentAsync(audioFragment);
                // Затем обрабатываем его
                return await session.ProcessFragmentAsync(fragmentId, cancellationToken);
            }

            throw new InvalidOperationException($"Сессия {sessionId} не найдена");
        }
        
        #region Методы управления очередью

        /// <summary>
        /// Добавляет фрагмент в очередь распознавания
        /// </summary>
        /// <param name="audioData">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет обработки (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <param name="processImmediately">Начать обработку немедленно, игнорируя очередь</param>
        /// <returns>Идентификатор фрагмента и Task для ожидания завершения обработки</returns>
        public async Task<(int FragmentId, Task<RecognitionResult> ProcessingTask)> EnqueueFragmentAsync(
            byte[] audioData, 
            int priority = 0, 
            string metadata = "", 
            Guid? sessionId = null,
            bool processImmediately = false)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                throw new ArgumentException("Указанная сессия не найдена", nameof(sessionId));
            }
            
            // Добавляем фрагмент в сессию
            int fragmentId = await session.AddFragmentAsync(audioData, priority, metadata);
            
            // Создаем CompletionSource для ожидания результата
            var tcs = new TaskCompletionSource<RecognitionResult>();
            
            // Обработчик события изменения статуса фрагмента
            FragmentStatusChangedEventArgs lastStatusChangeEvent = null;
            
            void StatusChangedHandler(object sender, FragmentStatusChangedEventArgs e)
            {
                if (e.SessionId == session.Id && e.FragmentId == fragmentId)
                {
                    // Сохраняем последнее событие изменения статуса
                    lastStatusChangeEvent = e;
                    
                    // Если статус изменился на завершенный или отмененный
                    if (e.NewStatus == FragmentStatus.Completed)
                    {
                        // Получаем фрагмент и устанавливаем результат
                        var fragment = session.GetFragment(fragmentId);
                        if (fragment != null && fragment.Result != null)
                        {
                            tcs.TrySetResult(fragment.Result);
                        }
                    }
                    else if (e.NewStatus == FragmentStatus.Failed)
                    {
                        tcs.TrySetException(new Exception("Ошибка при обработке фрагмента"));
                    }
                    else if (e.NewStatus == FragmentStatus.Canceled)
                    {
                        tcs.TrySetCanceled();
                    }
                }
            }
            
            // Подписываемся на событие изменения статуса
            FragmentStatusChanged += StatusChangedHandler;
            
            // Задача, ожидающая результат обработки
            var processingTask = tcs.Task.ContinueWith<RecognitionResult>(t => 
            {
                // Отписываемся от события изменения статуса
                FragmentStatusChanged -= StatusChangedHandler;
                
                // Если фрагмент был отменен или произошла ошибка
                if (t.IsCanceled)
                {
                    throw new OperationCanceledException();
                }
                else if (t.IsFaulted)
                {
                    throw t.Exception;
                }
                
                return t.Result;
            });
            
            // Если требуется немедленная обработка
            if (processImmediately)
            {
                // Запускаем обработку фрагмента
                _ = Task.Run(async () => 
                {
                    try
                    {
                        await session.ProcessFragmentAsync(fragmentId);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
            }
            
            return (fragmentId, processingTask);
        }
        
        // Словарь токенов отмены для фрагментов
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _fragmentCancellationTokens =
            new ConcurrentDictionary<Guid, CancellationTokenSource>();
            
        // Получает GUID для фрагмента на основе ID сессии и ID фрагмента
        private Guid GetFragmentGuid(Guid sessionId, int fragmentId)
        {
            // Создаем строку, которая будет хешироваться
            string input = $"{sessionId}:{fragmentId}";
            
            // Получаем байты строки
            byte[] inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
            
            // Создаем хеш
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hashBytes = md5.ComputeHash(inputBytes);
                
                // Заменяем последние 4 байта на ID фрагмента для обратной совместимости
                byte[] fragmentBytes = BitConverter.GetBytes(fragmentId);
                Array.Copy(fragmentBytes, 0, hashBytes, 12, 4);
                
                return new Guid(hashBytes);
            }
        }
        
        // Получает сессию и ID фрагмента из GUID фрагмента
        private (Guid SessionId, int FragmentId) GetSessionAndFragmentId(Guid fragmentGuid)
        {
            byte[] bytes = fragmentGuid.ToByteArray();
            
            // Получаем ID фрагмента из последних 4 байт
            int fragmentId = BitConverter.ToInt32(bytes, 12);
            
            // Перебираем все сессии и ищем ту, чей хеш совпадает
            foreach (var session in _sessions.Values)
            {
                var testGuid = GetFragmentGuid(session.Id, fragmentId);
                if (testGuid == fragmentGuid)
                {
                    return (session.Id, fragmentId);
                }
            }
            
            // Если сессия не найдена, возвращаем пустой GUID
            return (Guid.Empty, fragmentId);
        }
        
        /// <summary>
        /// Изменяет приоритет фрагмента
        /// </summary>
        /// <param name="fragmentGuid">Глобальный идентификатор фрагмента</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <returns>true, если приоритет успешно изменен</returns>
        public bool ChangeFragmentPriority(Guid fragmentGuid, int newPriority)
        {
            ThrowIfDisposed();
            
            var (sessionId, fragmentId) = GetSessionAndFragmentId(fragmentGuid);
                
            RecognitionSession session = GetSession(sessionId);
            if (session == null)
            {
                return false;
            }
            
            return session.ChangeFragmentPriority(fragmentId, newPriority);
        }
        
        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="fragmentGuid">Глобальный идентификатор фрагмента</param>
        /// <returns>true, если обработка успешно отменена</returns>
        public bool CancelFragment(Guid fragmentGuid)
        {
            ThrowIfDisposed();
            
            var (sessionId, fragmentId) = GetSessionAndFragmentId(fragmentGuid);
            
            // Отменяем токен для фрагмента, если он существует
            if (_fragmentCancellationTokens.TryRemove(fragmentGuid, out var cts))
            {
                try
                {
                    cts.Cancel();
                    cts.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Токен уже освобожден, игнорируем
                }
            }
            
            RecognitionSession session = GetSession(sessionId);
            if (session == null)
            {
                return false;
            }
            
            return session.CancelFragment(fragmentId);
        }
        
        /// <summary>
        /// Приостанавливает обработку фрагментов
        /// </summary>
        public void PauseProcessing()
        {
            ThrowIfDisposed();
            
            // Приостанавливаем обработку во всех сессиях
            foreach (var session in _sessions.Values)
            {
                session.PauseProcessing();
            }
        }
        
        /// <summary>
        /// Возобновляет обработку фрагментов
        /// </summary>
        public void ResumeProcessing()
        {
            ThrowIfDisposed();
            
            // Возобновляем обработку во всех сессиях
            foreach (var session in _sessions.Values)
            {
                session.ResumeProcessing();
            }
        }

        /// <summary>
        /// Отменяет все текущие операции распознавания
        /// </summary>
        public void CancelAllOperations()
        {
            ThrowIfDisposed();
            
            try
            {
                // Отменяем глобальный токен
                if (_globalCancellationSource != null)
                {
                    _globalCancellationSource.Cancel();
                    _globalCancellationSource.Dispose();
                    _globalCancellationSource = new CancellationTokenSource();
                }
                
                // Отменяем все токены сессий
                foreach (var cts in _sessionCancellationTokens.Values)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Токен уже освобожден, игнорируем
                    }
                }
                _sessionCancellationTokens.Clear();
                
                // Отменяем все токены фрагментов
                foreach (var cts in _fragmentCancellationTokens.Values)
                {
                    try
                    {
                        cts.Cancel();
                        cts.Dispose();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Токен уже освобожден, игнорируем
                    }
                }
                _fragmentCancellationTokens.Clear();
                
                // Сбрасываем статусы всех фрагментов на "Отменено"
                foreach (var session in _sessions.Values)
                {
                    session.CancelAllFragments();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отмене операций: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Очищает очередь фрагментов, ожидающих обработки
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <param name="cancelProcessing">Отменять ли текущие операции распознавания</param>
        public void ClearQueue(Guid? sessionId = null, bool cancelProcessing = false)
        {
            ThrowIfDisposed();
            
            if (sessionId.HasValue)
            {
                // Очищаем очередь конкретной сессии
                RecognitionSession session = GetSession(sessionId.Value);
            if (session != null)
                {
                    if (cancelProcessing)
                    {
                        // Отменяем токен сессии
                        if (_sessionCancellationTokens.TryGetValue(session.Id, out var cts))
                        {
                            try
                            {
                                cts.Cancel();
                                cts.Dispose();
                                _sessionCancellationTokens[session.Id] = new CancellationTokenSource();
                            }
                            catch (ObjectDisposedException)
                            {
                                // Токен уже освобожден, игнорируем
                            }
                        }
                        
                        // Отменяем все фрагменты в сессии
                        session.CancelAllFragments();
                    }
                    else
                    {
                        // Очищаем только ожидающие фрагменты
                        session.ClearPendingFragments();
                    }
                }
            }
            else
            {
                // Очищаем очередь всех сессий
                if (cancelProcessing)
                {
                    CancelAllOperations();
                }
                else
                {
                    foreach (var session in _sessions.Values)
                    {
                        session.ClearPendingFragments();
                    }
                }
            }
        }
        
        /// <summary>
        /// Получает глобальный идентификатор фрагмента
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>Глобальный идентификатор фрагмента</returns>
        public Guid GetFragmentGlobalId(Guid sessionId, int fragmentId)
        {
            return GetFragmentGuid(sessionId, fragmentId);
        }
        
        /// <summary>
        /// Получает информацию о фрагменте
        /// </summary>
        /// <param name="fragmentGuid">Глобальный идентификатор фрагмента</param>
        /// <returns>Фрагмент или null, если не найден</returns>
        public SessionFragment GetFragmentInfo(Guid fragmentGuid)
        {
            ThrowIfDisposed();
            
            var (sessionId, fragmentId) = GetSessionAndFragmentId(fragmentGuid);
            
            RecognitionSession session = GetSession(sessionId);
            if (session == null)
            {
                return null;
            }
            
            return session.GetFragment(fragmentId);
        }
        
        /// <summary>
        /// Получает информацию о фрагменте
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Информация о фрагменте или null, если не найден</returns>
        public SessionFragment GetFragment(int fragmentId, Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return null;
            }
            
            return session.GetFragment(fragmentId);
        }
        
        /// <summary>
        /// Получает список фрагментов в сессии
        /// </summary>
        /// <param name="status">Фильтр по статусу (null для всех фрагментов)</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Список фрагментов</returns>
        public IReadOnlyList<SessionFragment> GetFragments(FragmentStatus? status = null, Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return Array.Empty<SessionFragment>();
            }
            
            return session.GetFragments(status);
        }
        
        /// <summary>
        /// Получает количество фрагментов с указанным статусом
        /// </summary>
        /// <param name="status">Статус фрагментов (null для всех фрагментов)</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Количество фрагментов</returns>
        public int GetFragmentsCount(FragmentStatus? status = null, Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return 0;
            }
            
            return session.GetFragments(status).Count;
        }
        
        /// <summary>
        /// Повторно обрабатывает фрагмент, завершившийся с ошибкой
        /// </summary>
        /// <param name="fragmentGuid">Глобальный идентификатор фрагмента</param>
        /// <returns>True если фрагмент поставлен на повторную обработку, иначе false</returns>
        public bool RetryFailedFragment(Guid fragmentGuid)
        {
            return RetryFailedFragment(fragmentGuid, CancellationToken.None);
        }
        
        /// <summary>
        /// Повторно обрабатывает фрагмент, завершившийся с ошибкой
        /// </summary>
        /// <param name="fragmentGuid">Глобальный идентификатор фрагмента</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>True если фрагмент поставлен на повторную обработку, иначе false</returns>
        public bool RetryFailedFragment(Guid fragmentGuid, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            
            var (sessionId, fragmentId) = GetSessionAndFragmentId(fragmentGuid);
            
            RecognitionSession session = GetSession(sessionId);
            if (session == null)
            {
                return false;
            }
            
            return session.RetryFailedFragment(fragmentId, cancellationToken);
        }
        
        /// <summary>
        /// Повторно обрабатывает все фрагменты в сессии, завершившиеся с ошибкой
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Количество фрагментов, поставленных на повторную обработку</returns>
        public int RetryAllFailedFragments(Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return 0;
            }
            
            return session.RetryAllFailedFragments();
        }
        
        /// <summary>
        /// Получает статистику по обработке фрагментов в сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Словарь со статистикой по каждому статусу</returns>
        public Dictionary<FragmentStatus, int> GetFragmentsStatistics(Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return new Dictionary<FragmentStatus, int>();
            }
            
            var result = new Dictionary<FragmentStatus, int>
            {
                { FragmentStatus.Pending, session.GetFragmentsCount(FragmentStatus.Pending) },
                { FragmentStatus.Processing, session.GetFragmentsCount(FragmentStatus.Processing) },
                { FragmentStatus.Completed, session.GetFragmentsCount(FragmentStatus.Completed) },
                { FragmentStatus.Failed, session.GetFragmentsCount(FragmentStatus.Failed) },
                { FragmentStatus.Canceled, session.GetFragmentsCount(FragmentStatus.Canceled) }
            };
            
            return result;
        }
        
        #endregion

        /// <summary>
        /// Асинхронно освобождает ресурсы
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            // Отменяем все операции
            try
            {
                CancelAllOperations();
            }
            catch
            {
                // Игнорируем исключения при отмене
            }

            // Ожидаем завершение всех операций
            try
            {
                // Даем небольшую задержку для завершения операций
                await Task.Delay(1000);
            }
            catch (TaskCanceledException)
            {
                // Игнорируем исключение отмены
            }

            Dispose();
            GC.SuppressFinalize(this);
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

            _isDisposed = true;

            // Освобождаем все сессии
            foreach (var session in _sessions.Values)
            {
                try
                {
                    session.Dispose();
                }
                catch
                {
                    // Игнорируем исключения при освобождении ресурсов
                }
            }

            _sessions.Clear();
            _sessionCancellationTokens.Clear();
            
            // Отменяем все операции
            _globalCancellationSource.Cancel();
            _globalCancellationSource.Dispose();
        }
        
        /// <summary>
        /// Выбрасывает исключение, если объект был освобожден
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SessionManager));
            }
        }
    }
} 