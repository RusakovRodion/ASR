namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Настройки модели Whisper
    /// </summary>
    public class WhisperModelSettings : IModelSettings
    {
        /// <summary>
        /// Тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Whisper;

        /// <summary>
        /// Путь к файлу модели
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// Код языка для распознавания
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// Тип модели (tiny, base)
        /// </summary>
        public string ModelType { get; }

        /// <summary>
        /// Создает новый экземпляр настроек модели Whisper
        /// </summary>
        /// <param name="modelPath">Путь к файлу модели</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="modelType">Тип модели (tiny, base)</param>
        public WhisperModelSettings(string modelPath, string language = "ru", string modelType = "base")
        {
            ModelPath = modelPath;
            Language = string.IsNullOrEmpty(language) ? "ru" : language;
            ModelType = string.IsNullOrEmpty(modelType) ? "base" : modelType;
        }
    }
} 