using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Events;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Sessions;

namespace SpeechRecognition.Core
{
    public class SessionManager
    {
        private readonly ISpeechRecognizer _recognizer;
        private readonly ILogger _logger;
        private readonly ConcurrentQueue<RecognitionSession> _sessions;
        private readonly object _lock = new object();
        private bool _isProcessing;

        // События для оповещения о статусе распознавания
        public event EventHandler<RecognitionEventArgs> RecognitionStarted;
        public event EventHandler<RecognitionEventArgs> RecognitionCompleted;
        public event EventHandler<FragmentStatusChangedEventArgs> FragmentStatusChanged;
        public event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        /// <summary>
        /// Создает экземпляр менеджера сессий
        /// </summary>
        /// <param name="recognizer">Распознаватель речи</param>
        /// <param name="logger">Логгер</param>
        public SessionManager(ISpeechRecognizer recognizer, ILogger logger)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _sessions = new ConcurrentQueue<RecognitionSession>();
            _isProcessing = false;
        }

        // ... existing code ...
    }
} 