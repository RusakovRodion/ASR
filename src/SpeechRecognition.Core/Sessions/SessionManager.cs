using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Events;

namespace SpeechRecognition.Core.Sessions
{
    /// <summary>
    /// Менеджер сессий распознавания речи
    /// </summary>
    public class SessionManager : IDisposable
    {
        private readonly ISpeechRecognizer _recognizer;
        private readonly ConcurrentDictionary<Guid, RecognitionSession> _sessions;
        private bool _isDisposed;
        private RecognitionSession _defaultSession;

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
        /// Создает новый экземпляр класса SessionManager
        /// </summary>
        /// <param name="recognizer">Распознаватель речи, который будет использоваться для всех сессий</param>
        public SessionManager(ISpeechRecognizer recognizer)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _sessions = new ConcurrentDictionary<Guid, RecognitionSession>();
            _isDisposed = false;
            
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

            var session = new RecognitionSession(_recognizer);
            
            // Подписываемся на события сессии
            session.RecognitionStarted += OnSessionRecognitionStarted;
            session.RecognitionCompleted += OnSessionRecognitionCompleted;
            session.FragmentStatusChanged += OnSessionFragmentStatusChanged;
            
            _sessions.TryAdd(session.Id, session);
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

            return _sessions.Values.ToList();
        }

        /// <summary>
        /// Обрабатывает аудиофрагмент в рамках указанной сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="audioFragment">Аудиофрагмент</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания или null, если сессия не найдена</returns>
        public async Task<RecognitionResult> ProcessFragmentAsync(Guid sessionId, byte[] audioFragment, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (audioFragment == null || audioFragment.Length == 0)
            {
                throw new ArgumentException("Аудиофрагмент не может быть пустым", nameof(audioFragment));
            }

            if (_sessions.TryGetValue(sessionId, out var session))
            {
                return await session.ProcessFragmentAsync(audioFragment, cancellationToken);
            }

            return null;
        }
        
        #region Методы управления очередью

        /// <summary>
        /// Добавляет фрагмент в очередь распознавания
        /// </summary>
        /// <param name="audioData">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет обработки (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>Идентификатор фрагмента</returns>
        public async Task<int> EnqueueFragmentAsync(byte[] audioData, int priority = 0, string metadata = "", Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                throw new ArgumentException("Указанная сессия не найдена", nameof(sessionId));
            }
            
            return await session.AddFragmentAsync(audioData, priority, metadata);
        }
        
        /// <summary>
        /// Изменяет приоритет фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>true, если приоритет успешно изменен</returns>
        public bool ChangeFragmentPriority(int fragmentId, int newPriority, Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return false;
            }
            
            return session.ChangeFragmentPriority(fragmentId, newPriority);
        }
        
        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        /// <returns>true, если обработка успешно отменена</returns>
        public bool CancelFragment(int fragmentId, Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session == null)
            {
                return false;
            }
            
            return session.CancelFragment(fragmentId);
        }
        
        /// <summary>
        /// Приостанавливает обработку фрагментов в сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        public void PauseProcessing(Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session != null)
            {
                session.Pause();
            }
        }
        
        /// <summary>
        /// Возобновляет обработку фрагментов в сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        public void ResumeProcessing(Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session != null)
            {
                session.Resume();
            }
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
        /// Очищает ожидающие фрагменты в сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии (null для использования сессии по умолчанию)</param>
        public void ClearPendingFragments(Guid? sessionId = null)
        {
            ThrowIfDisposed();
            
            RecognitionSession session = sessionId.HasValue ? 
                GetSession(sessionId.Value) : 
                _defaultSession;
                
            if (session != null)
            {
                session.ClearPendingFragments();
            }
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
        
        #endregion

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            foreach (var session in _sessions.Values)
            {
                // Отписываемся от событий сессии
                session.RecognitionStarted -= OnSessionRecognitionStarted;
                session.RecognitionCompleted -= OnSessionRecognitionCompleted;
                session.FragmentStatusChanged -= OnSessionFragmentStatusChanged;
                
                session.Dispose();
            }

            _sessions.Clear();
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(SessionManager));
            }
        }
    }
} 