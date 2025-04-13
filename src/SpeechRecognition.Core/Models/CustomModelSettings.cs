using System.Collections.Generic;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Настройки пользовательской модели распознавания
    /// </summary>
    public class CustomModelSettings : IModelSettings
    {
        /// <summary>
        /// Тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Custom;

        /// <summary>
        /// Путь к файлу модели
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// Код языка для распознавания
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// Дополнительные параметры для пользовательской модели
        /// </summary>
        public Dictionary<string, string> AdditionalParameters { get; }

        /// <summary>
        /// Создает новый экземпляр настроек пользовательской модели
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="additionalParameters">Дополнительные параметры модели</param>
        public CustomModelSettings(
            string modelPath, 
            string language = "ru",
            Dictionary<string, string> additionalParameters = null)
        {
            ModelPath = modelPath;
            Language = string.IsNullOrEmpty(language) ? "ru" : language;
            AdditionalParameters = additionalParameters ?? new Dictionary<string, string>();
        }
    }
} 