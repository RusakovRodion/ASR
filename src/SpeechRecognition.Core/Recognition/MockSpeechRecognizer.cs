using System;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Мок-класс для тестирования распознавателя речи
    /// </summary>
    public class MockSpeechRecognizer : BaseSpeechRecognizer
    {
        private readonly string _mockResponse;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public override RecognizerType RecognizerType => RecognizerType.Mock;

        /// <summary>
        /// Создает новый экземпляр моковой реализации распознавателя речи
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио для подготовки данных</param>
        /// <param name="modelSettings">Настройки модели</param>
        /// <param name="mockResponse">Фиксированный ответ для тестирования</param>
        public MockSpeechRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings, string mockResponse = "Тестовый результат распознавания речи") 
            : base(audioProcessor, modelSettings)
        {
            _mockResponse = mockResponse;
        }

        /// <summary>
        /// Инициализирует мок-распознаватель речи
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        public override Task InitializeAsync()
        {
            _isInitialized = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Имитирует распознавание речи, возвращая фиксированный ответ
        /// </summary>
        /// <param name="audioData">Аудиоданные (не используются)</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public override Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            // Имитируем задержку распознавания
            return Task.FromResult(_mockResponse);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
} 