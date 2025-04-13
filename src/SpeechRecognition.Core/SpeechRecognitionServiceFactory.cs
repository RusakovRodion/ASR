using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recognition;

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Фабрика для создания экземпляров сервиса распознавания речи
    /// </summary>
    public static class SpeechRecognitionServiceFactory
    {
        /// <summary>
        /// Создает новый экземпляр сервиса распознавания речи с использованием Whisper
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

            var modelSettings = new WhisperModelSettings(modelPath, language, modelType);
            return CreateService(logger, modelSettings);
        }

        /// <summary>
        /// Создает новый экземпляр сервиса распознавания речи с указанными настройками модели
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>Сервис распознавания речи</returns>
        public static ISpeechRecognitionService CreateService(
            ILogger logger,
            IModelSettings modelSettings)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (modelSettings == null)
                throw new ArgumentNullException(nameof(modelSettings));

            var audioProcessor = new AudioProcessor();
            var recognizer = SpeechRecognizerFactory.CreateRecognizer(audioProcessor, modelSettings, logger);
            return new WhisperStreamProcessor(logger, recognizer);
        }

        /// <summary>
        /// Создает новый экземпляр сервиса распознавания речи для тестирования
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="mockResponse">Фиксированный ответ для тестирования</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <returns>Сервис распознавания речи для тестирования</returns>
        public static ISpeechRecognitionService CreateMockService(
            ILogger logger,
            string mockResponse = "Тестовый результат распознавания речи",
            string language = "ru")
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            var modelSettings = new MockModelSettings(language, mockResponse);
            return CreateService(logger, modelSettings);
        }

        /// <summary>
        /// Создает новый экземпляр сервиса распознавания речи с пользовательской моделью
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="modelPath">Путь к файлу модели</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="additionalParameters">Дополнительные параметры модели</param>
        /// <returns>Сервис распознавания речи</returns>
        public static ISpeechRecognitionService CreateCustomService(
            ILogger logger,
            string modelPath,
            string language = "ru",
            Dictionary<string, string> additionalParameters = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));

            if (string.IsNullOrEmpty(modelPath))
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));

            var modelSettings = new CustomModelSettings(modelPath, language, additionalParameters);
            return CreateService(logger, modelSettings);
        }
    }
} 