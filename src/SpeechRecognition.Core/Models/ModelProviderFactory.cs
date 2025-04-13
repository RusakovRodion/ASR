using System;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Фабрика для создания провайдеров моделей
    /// </summary>
    public static class ModelProviderFactory
    {
        /// <summary>
        /// Создает провайдер моделей для указанного типа распознавателя
        /// </summary>
        /// <param name="recognizerType">Тип распознавателя</param>
        /// <param name="logger">Логгер</param>
        /// <returns>Провайдер моделей</returns>
        public static IModelProvider CreateProvider(RecognizerType recognizerType, ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            return recognizerType switch
            {
                RecognizerType.Whisper => new WhisperModelProvider(logger),
                RecognizerType.Mock => new MockModelProvider(logger),
                RecognizerType.Custom => new CustomModelProvider(logger),
                _ => throw new ArgumentException($"Неподдерживаемый тип распознавателя: {recognizerType}")
            };
        }

        /// <summary>
        /// Создает провайдер моделей на основе настроек модели
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <param name="logger">Логгер</param>
        /// <returns>Провайдер моделей</returns>
        public static IModelProvider CreateProvider(IModelSettings modelSettings, ILogger logger)
        {
            if (modelSettings == null)
                throw new ArgumentNullException(nameof(modelSettings));

            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            return CreateProvider(modelSettings.RecognizerType, logger);
        }
    }
} 