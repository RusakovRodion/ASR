namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Типы распознавателей речи
    /// </summary>
    public enum RecognizerType
    {
        /// <summary>
        /// Whisper модель от OpenAI
        /// </summary>
        Whisper,
        
        /// <summary>
        /// Заглушка для тестирования
        /// </summary>
        Mock,
        
        /// <summary>
        /// Пользовательская модель
        /// </summary>
        Custom
    }
} 