using System;
using SpeechRecognition.Core.Sessions;

namespace SpeechRecognition.Core.Events
{
    /// <summary>
    /// Аргументы события распознавания речи
    /// </summary>
    public class RecognitionEventArgs : EventArgs
    {
        /// <summary>
        /// Результат распознавания
        /// </summary>
        public RecognitionResult Result { get; }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionEventArgs
        /// </summary>
        /// <param name="result">Результат распознавания</param>
        public RecognitionEventArgs(RecognitionResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
        }
    }
} 