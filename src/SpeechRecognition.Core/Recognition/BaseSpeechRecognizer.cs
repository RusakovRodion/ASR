using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Базовый класс для распознавателей речи
    /// </summary>
    public abstract class BaseSpeechRecognizer : ISpeechRecognizer
    {
        protected readonly IAudioProcessor _audioProcessor;
        protected bool _isInitialized;
        protected bool _isDisposed;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public abstract RecognizerType RecognizerType { get; }

        /// <summary>
        /// Настройки модели
        /// </summary>
        public IModelSettings ModelSettings { get; }

        /// <summary>
        /// Создает новый экземпляр базового распознавателя речи
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио для подготовки данных</param>
        /// <param name="modelSettings">Настройки модели</param>
        protected BaseSpeechRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings)
        {
            _audioProcessor = audioProcessor ?? throw new ArgumentNullException(nameof(audioProcessor));
            ModelSettings = modelSettings ?? throw new ArgumentNullException(nameof(modelSettings));
            _isInitialized = false;
            _isDisposed = false;
        }

        /// <summary>
        /// Инициализирует распознаватель речи
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        public abstract Task InitializeAsync();

        /// <summary>
        /// Распознает речь из аудиоданных
        /// </summary>
        /// <param name="audioData">Аудиоданные в формате WAV</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public abstract Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Распознает речь из аудиофайла
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public virtual async Task<string> RecognizeSpeechFromFileAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(audioFilePath))
            {
                throw new ArgumentException("Путь к аудиофайлу не может быть пустым", nameof(audioFilePath));
            }

            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Аудиофайл не найден", audioFilePath);
            }

            // Подготавливаем аудиоданные из файла
            byte[] audioData = await _audioProcessor.PrepareAudioFileAsync(audioFilePath);
            
            // Распознаем речь
            return await RecognizeSpeechAsync(audioData, cancellationToken);
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public abstract void Dispose();

        /// <summary>
        /// Проверяет, не был ли объект уже освобожден
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }
    }
} 