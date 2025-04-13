using System;

namespace SpeechRecognition.Core.Events
{
    /// <summary>
    /// Аргументы события ошибки распознавания
    /// </summary>
    public class RecognitionErrorEventArgs : EventArgs
    {
        /// <summary>
        /// Исключение, вызвавшее ошибку
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Описание ошибки
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Идентификатор фрагмента, при обработке которого произошла ошибка
        /// </summary>
        public int? FragmentId { get; }

        /// <summary>
        /// Создает новый экземпляр аргументов события ошибки распознавания
        /// </summary>
        /// <param name="exception">Исключение, вызвавшее ошибку</param>
        /// <param name="errorMessage">Описание ошибки</param>
        /// <param name="fragmentId">Идентификатор фрагмента (если применимо)</param>
        public RecognitionErrorEventArgs(Exception exception, string errorMessage, int? fragmentId = null)
        {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            ErrorMessage = errorMessage ?? exception.Message;
            FragmentId = fragmentId;
        }
    }
} 