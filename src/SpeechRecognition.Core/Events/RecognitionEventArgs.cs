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
        /// Текст распознавания (если Result не указан)
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Размер аудиоданных в байтах
        /// </summary>
        public int AudioSize { get; }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionEventArgs
        /// </summary>
        /// <param name="result">Результат распознавания</param>
        public RecognitionEventArgs(RecognitionResult result)
        {
            Result = result ?? throw new ArgumentNullException(nameof(result));
            Text = result?.Text ?? string.Empty;
            AudioSize = 0;
        }

        /// <summary>
        /// Создает новый экземпляр класса RecognitionEventArgs с текстом и размером аудио
        /// </summary>
        /// <param name="text">Текст распознавания</param>
        /// <param name="audioSize">Размер аудиоданных в байтах</param>
        public RecognitionEventArgs(string text, int audioSize)
        {
            Result = null;
            Text = text ?? string.Empty;
            AudioSize = audioSize;
        }
    }
} 