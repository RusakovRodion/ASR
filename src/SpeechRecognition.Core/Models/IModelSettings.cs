namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Интерфейс для настроек моделей распознавания речи
    /// </summary>
    public interface IModelSettings
    {
        /// <summary>
        /// Тип распознавателя
        /// </summary>
        RecognizerType RecognizerType { get; }

        /// <summary>
        /// Путь к файлу модели
        /// </summary>
        string ModelPath { get; }

        /// <summary>
        /// Код языка для распознавания
        /// </summary>
        string Language { get; }
    }
} 