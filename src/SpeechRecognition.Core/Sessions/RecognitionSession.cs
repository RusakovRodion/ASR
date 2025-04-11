using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Recognition;

namespace SpeechRecognition.Core.Sessions
{
    /// <summary>
    /// Представляет сессию распознавания речи
    /// </summary>
    public class RecognitionSession : IDisposable
    {
        private readonly ISpeechRecognizer _recognizer;
        private readonly Dictionary<int, RecognitionResult> _results;
        private int _nextFragmentId;
        private bool _isDisposed;

        /// <summary>
        /// Идентификатор сессии
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Время создания сессии
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Количество обработанных фрагментов
        /// </summary>
        public int ProcessedFragmentsCount => _results.Count;

        /// <summary>
        /// Создает новый экземпляр класса RecognitionSession
        /// </summary>
        /// <param name="recognizer">Распознаватель речи</param>
        public RecognitionSession(ISpeechRecognizer recognizer)
        {
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            _results = new Dictionary<int, RecognitionResult>();
            _nextFragmentId = 0;
            _isDisposed = false;
            
            Id = Guid.NewGuid();
            CreatedAt = DateTime.Now;
        }

        /// <summary>
        /// Распознает речь из фрагмента аудиоданных
        /// </summary>
        /// <param name="audioFragment">Фрагмент аудиоданных</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания</returns>
        public async Task<RecognitionResult> ProcessFragmentAsync(byte[] audioFragment, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (audioFragment == null || audioFragment.Length == 0)
            {
                throw new ArgumentException("Фрагмент аудиоданных не может быть пустым", nameof(audioFragment));
            }

            int fragmentId = Interlocked.Increment(ref _nextFragmentId) - 1;
            DateTime startTime = DateTime.Now;

            // Распознавание речи
            string recognizedText = await _recognizer.RecognizeSpeechAsync(audioFragment, cancellationToken);

            DateTime endTime = DateTime.Now;

            // Создание результата
            var result = new RecognitionResult(Id, fragmentId, recognizedText, startTime, endTime);

            // Сохранение результата
            lock (_results)
            {
                _results[fragmentId] = result;
            }

            return result;
        }

        /// <summary>
        /// Получает результат распознавания по идентификатору фрагмента
        /// </summary>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <returns>Результат распознавания, или null если не найден</returns>
        public RecognitionResult GetResult(int fragmentId)
        {
            ThrowIfDisposed();

            lock (_results)
            {
                if (_results.TryGetValue(fragmentId, out var result))
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Получает все результаты распознавания в рамках сессии
        /// </summary>
        /// <returns>Коллекция результатов распознавания</returns>
        public IReadOnlyCollection<RecognitionResult> GetAllResults()
        {
            ThrowIfDisposed();

            lock (_results)
            {
                var resultsCopy = new List<RecognitionResult>(_results.Values);
                return resultsCopy;
            }
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