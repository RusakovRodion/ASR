using System;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Интерфейс для распознавания речи
    /// </summary>
    public interface ISpeechRecognizer : IDisposable
    {
        /// <summary>
        /// Инициализирует распознаватель речи
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        Task InitializeAsync();

        /// <summary>
        /// Распознает речь из аудиоданных
        /// </summary>
        /// <param name="audioData">Аудиоданные в формате WAV</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default);

        /// <summary>
        /// Распознает речь из аудиофайла
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        Task<string> RecognizeSpeechFromFileAsync(string audioFilePath, CancellationToken cancellationToken = default);
    }
} 