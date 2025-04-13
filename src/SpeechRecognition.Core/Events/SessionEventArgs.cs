using System;

namespace SpeechRecognition.Core.Events
{
    /// <summary>
    /// Аргументы события, связанного с сессией
    /// </summary>
    public class SessionEventArgs : EventArgs
    {
        /// <summary>
        /// Идентификатор сессии
        /// </summary>
        public Guid SessionId { get; }

        /// <summary>
        /// Создает новые аргументы события сессии
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        public SessionEventArgs(Guid sessionId)
        {
            SessionId = sessionId;
        }
    }
} 