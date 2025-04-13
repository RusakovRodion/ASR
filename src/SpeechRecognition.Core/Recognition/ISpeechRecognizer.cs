using System;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Models;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Интерфейс для распознавания речи
    /// </summary>
    public interface ISpeechRecognizer : IDisposable
    {
        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        RecognizerType RecognizerType { get; }

        /// <summary>
        /// Получает настройки модели
        /// </summary>
        IModelSettings ModelSettings { get; }

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