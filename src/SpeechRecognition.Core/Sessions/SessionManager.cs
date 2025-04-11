using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;

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

        /// <summary>
        /// Создает новый экземпляр класса SessionManager
        /// </summary>
        /// <param name="recognizer">Распознаватель речи, который будет использоваться для всех сессий</param>
        public SessionManager(ISpeechRecognizer recognizer)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _sessions = new ConcurrentDictionary<Guid, RecognitionSession>();
            _isDisposed = false;
        }

        /// <summary>
        /// Создает новую сессию распознавания
        /// </summary>
        /// <returns>Созданная сессия</returns>
        public RecognitionSession CreateSession()
        {
            ThrowIfDisposed();

            var session = new RecognitionSession(_recognizer);
            _sessions.TryAdd(session.Id, session);
            return session;
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

            if (_sessions.TryRemove(sessionId, out var session))
            {
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