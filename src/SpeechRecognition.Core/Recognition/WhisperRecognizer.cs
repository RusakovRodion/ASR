using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recovery;
using Whisper.net;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Распознаватель речи, использующий модель Whisper от OpenAI
    /// </summary>
    public class WhisperRecognizer : BaseSpeechRecognizer
    {
        private readonly ILogger _logger;
        private WhisperModelSettings _whisperSettings;
        private WhisperFactory _whisperFactory;
        private WhisperProcessor _whisperProcessor;
        private RetryPolicy _retryPolicy;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public override RecognizerType RecognizerType => RecognizerType.Whisper;

        /// <summary>
        /// Создает новый экземпляр класса WhisperRecognizer
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио для подготовки данных</param>
        /// <param name="modelSettings">Настройки модели</param>
        /// <param name="logger">Логгер</param>
        public WhisperRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings, ILogger logger) 
            : base(audioProcessor, modelSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (modelSettings is not WhisperModelSettings)
            {
                throw new ArgumentException("Требуются настройки модели Whisper", nameof(modelSettings));
            }
            
            _whisperSettings = (WhisperModelSettings)modelSettings;
            _retryPolicy = RetryPolicyFactory.CreateForSpeechRecognition(logger);
        }

        /// <summary>
        /// Создает новый экземпляр класса WhisperRecognizer
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио для подготовки данных</param>
        /// <param name="modelPath">Путь к файлу модели Whisper</param>
        /// <param name="language">Код языка (по умолчанию "ru" - русский)</param>
        /// <param name="modelType">Тип модели (tiny, base)</param>
        /// <param name="logger">Логгер</param>
        public WhisperRecognizer(IAudioProcessor audioProcessor, string modelPath, string language, string modelType, ILogger logger)
            : base(audioProcessor, new WhisperModelSettings(modelPath, language, modelType))
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _whisperSettings = (WhisperModelSettings)ModelSettings;
            _retryPolicy = RetryPolicyFactory.CreateForSpeechRecognition(logger);
        }

        /// <summary>
        /// Инициализирует распознаватель речи, загружая модель Whisper
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        public override async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            ThrowIfDisposed();

            if (!File.Exists(_whisperSettings.ModelPath))
            {
                throw new FileNotFoundException("Файл модели не найден", _whisperSettings.ModelPath);
            }

            // Используем политику восстановления для инициализации модели
            var initializationPolicy = RetryPolicyFactory.CreateForModelInitialization(_logger);
            
            await initializationPolicy.ExecuteAsync(async (cancellationToken) =>
            {
                try
                {
                    _whisperFactory = WhisperFactory.FromPath(_whisperSettings.ModelPath);
                    _whisperProcessor = _whisperFactory.CreateBuilder()
                        .WithLanguage(_whisperSettings.Language)
                        .Build();

                    _isInitialized = true;
                    _logger.LogInformation($"Модель Whisper успешно инициализирована: {_whisperSettings.ModelPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при инициализации модели Whisper");
                    throw new InvalidOperationException("Ошибка при инициализации модели Whisper", ex);
                }
            }, "Инициализация модели Whisper");
        }

        /// <summary>
        /// Распознает речь из аудиоданных
        /// </summary>
        /// <param name="audioData">Аудиоданные</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public override async Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!_isInitialized)
            {
                throw new InvalidOperationException("Распознаватель речи не инициализирован. Вызовите InitializeAsync() перед использованием.");
            }

            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioData));
            }

            // Используем политику восстановления для распознавания речи
            return await _retryPolicy.ExecuteAsync(async (token) =>
            {
                try
                {
                    using var audioStream = new MemoryStream(audioData);
                    var result = new System.Text.StringBuilder();
                    
                    await foreach (var segment in _whisperProcessor.ProcessAsync(audioStream, token))
                    {
                        result.Append(segment.Text);
                    }
                    
                    var recognizedText = result.ToString();
                    _logger.LogDebug($"Распознано успешно: {recognizedText.Substring(0, Math.Min(50, recognizedText.Length))}...");
                    return recognizedText;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при распознавании речи");
                    throw;
                }
            }, "Распознавание речи Whisper", cancellationToken);
        }

        /// <summary>
        /// Распознает речь из аудиофайла
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public override async Task<string> RecognizeSpeechFromFileAsync(string audioFilePath, CancellationToken cancellationToken = default)
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

            // Загружаем аудиофайл
            var audioData = await File.ReadAllBytesAsync(audioFilePath, cancellationToken);
            
            // Проверяем формат файла и при необходимости преобразуем
            var processedAudio = await _audioProcessor.PrepareAudioFileAsync(audioFilePath);
            
            // Выполняем распознавание с поддержкой восстановления
            return await RecognizeSpeechAsync(processedAudio, cancellationToken);
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

            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();

            _isDisposed = true;
            GC.SuppressFinalize(this);
        }
    }
} 