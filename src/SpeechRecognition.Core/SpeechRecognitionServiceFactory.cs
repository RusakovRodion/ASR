using System;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Фабрика для создания экземпляров сервиса распознавания речи
    /// </summary>
    public static class SpeechRecognitionServiceFactory
    {
        /// <summary>
        /// Создает новый экземпляр сервиса распознавания речи
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="modelPath">Путь к файлу модели</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="modelType">Тип модели (tiny, base)</param>
        /// <returns>Сервис распознавания речи</returns>
        public static ISpeechRecognitionService CreateService(
            ILogger logger, 
            string modelPath, 
            string language = "ru", 
            string modelType = "base")
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(modelPath))
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));

            return new WhisperStreamProcessor(logger, modelPath, language, modelType);
        }
    }
} 