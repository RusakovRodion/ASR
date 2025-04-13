using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Провайдер моделей Whisper
    /// </summary>
    public class WhisperModelProvider : IModelProvider
    {
        private readonly ILogger _logger;
        private readonly ModelDownloader _modelDownloader;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Whisper;

        /// <summary>
        /// Создает новый экземпляр провайдера моделей Whisper
        /// </summary>
        /// <param name="logger">Логгер</param>
        public WhisperModelProvider(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _modelDownloader = new ModelDownloader(logger);
        }

        /// <summary>
        /// Проверяет наличие модели и загружает её при необходимости
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>Путь к загруженной модели</returns>
        public async Task<string> EnsureModelExistsAsync(IModelSettings modelSettings)
        {
            if (!IsCompatible(modelSettings))
            {
                throw new ArgumentException("Несовместимые настройки модели", nameof(modelSettings));
            }

            var whisperSettings = (WhisperModelSettings)modelSettings;
            
            // Проверяем существование файла модели
            if (File.Exists(whisperSettings.ModelPath))
            {
                _logger.LogInformation($"Модель найдена: {whisperSettings.ModelPath}");
                return whisperSettings.ModelPath;
            }

            // Загружаем модель при необходимости
            return await _modelDownloader.EnsureModelExistsAsync(
                whisperSettings.ModelPath,
                whisperSettings.ModelType);
        }

        /// <summary>
        /// Проверяет совместимость настроек с данным провайдером
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>true, если настройки совместимы</returns>
        public bool IsCompatible(IModelSettings modelSettings)
        {
            return modelSettings is WhisperModelSettings && modelSettings.RecognizerType == RecognizerType.Whisper;
        }
    }
} 