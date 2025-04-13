namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Настройки моковой модели для тестирования
    /// </summary>
    public class MockModelSettings : IModelSettings
    {
        /// <summary>
        /// Тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Mock;

        /// <summary>
        /// Путь к файлу модели (не используется)
        /// </summary>
        public string ModelPath { get; } = "mock-model-path";

        /// <summary>
        /// Код языка для распознавания
        /// </summary>
        public string Language { get; }

        /// <summary>
        /// Фиксированный ответ для тестирования
        /// </summary>
        public string MockResponse { get; }

        /// <summary>
        /// Создает новый экземпляр настроек моковой модели
        /// </summary>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="mockResponse">Фиксированный ответ для тестирования</param>
        public MockModelSettings(string language = "ru", string mockResponse = "Тестовый результат распознавания речи")
        {
            Language = string.IsNullOrEmpty(language) ? "ru" : language;
            MockResponse = mockResponse;
        }
    }
} 