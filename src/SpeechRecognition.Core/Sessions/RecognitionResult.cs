using System;

namespace SpeechRecognition.Core.Sessions
{
    /// <summary>
    /// Представляет результат распознавания речи
    /// </summary>
    public class RecognitionResult
    {
        /// <summary>
        /// Идентификатор результата
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// Идентификатор сессии, в рамках которой было выполнено распознавание
        /// </summary>
        public Guid SessionId { get; }

        /// <summary>
        /// Идентификатор фрагмента в рамках сессии
        /// </summary>
        public int FragmentId { get; }

        /// <summary>
        /// Распознанный текст
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Время начала обработки фрагмента
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        /// Время окончания обработки фрагмента
        /// </summary>
        public DateTime EndTime { get; }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionResult
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="text">Распознанный текст</param>
        /// <param name="startTime">Время начала обработки</param>
        /// <param name="endTime">Время окончания обработки</param>
        public RecognitionResult(Guid sessionId, int fragmentId, string text, DateTime startTime, DateTime endTime)
        {
            Id = Guid.NewGuid();
            SessionId = sessionId;
            FragmentId = fragmentId;
            Text = text ?? string.Empty;
            StartTime = startTime;
            EndTime = endTime;
        }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionResult с текущими временными метками
        /// </summary>
        /// <param name="sessionId">Идентификатор сессии</param>
        /// <param name="fragmentId">Идентификатор фрагмента</param>
        /// <param name="text">Распознанный текст</param>
        public RecognitionResult(Guid sessionId, int fragmentId, string text)
            : this(sessionId, fragmentId, text, DateTime.Now, DateTime.Now)
        {
        }

        /// <summary>
        /// Возвращает строковое представление результата распознавания
        /// </summary>
        /// <returns>Строковое представление результата</returns>
        public override string ToString()
        {
            return $"Результат #{FragmentId} сессии {SessionId}: {Text}";
        }
    }
} 