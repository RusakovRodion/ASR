using System;
using System.IO;
using System.Threading.Tasks;

namespace SpeechRecognition.Core.Audio
{
    /// <summary>
    /// Интерфейс для обработки аудиоданных перед распознаванием речи
    /// </summary>
    public interface IAudioProcessor
    {
        /// <summary>
        /// Подготавливает аудиоданные для распознавания
        /// </summary>
        /// <param name="audioData">Исходные аудиоданные (PCM или WAV)</param>
        /// <returns>Подготовленные аудиоданные в формате WAV, готовые для распознавания</returns>
        Task<byte[]> PrepareAudioDataAsync(byte[] audioData);

        /// <summary>
        /// Подготавливает аудиофайл для распознавания
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <returns>Подготовленные аудиоданные в формате WAV, готовые для распознавания</returns>
        Task<byte[]> PrepareAudioFileAsync(string audioFilePath);

        /// <summary>
        /// Проверяет, является ли массив байтов WAV-файлом
        /// </summary>
        /// <param name="data">Проверяемые данные</param>
        /// <returns>true, если данные имеют заголовок WAV-файла</returns>
        bool IsWavFormat(byte[] data);

        /// <summary>
        /// Добавляет WAV-заголовок к PCM-данным
        /// </summary>
        /// <param name="pcmData">PCM-данные без заголовка</param>
        /// <param name="sampleRate">Частота дискретизации (по умолчанию 16000 Гц)</param>
        /// <param name="bitsPerSample">Бит на семпл (по умолчанию 16 бит)</param>
        /// <param name="channels">Количество каналов (по умолчанию 1 - моно)</param>
        /// <returns>Данные с WAV-заголовком</returns>
        byte[] AddWavHeader(byte[] pcmData, int sampleRate = 16000, int bitsPerSample = 16, int channels = 1);

        /// <summary>
        /// Проверяет, является ли массив байтов WAV-файлом
        /// </summary>
        /// <param name="data">Проверяемые данные</param>
        /// <returns>true, если данные имеют заголовок WAV-файла</returns>
        bool IsWavFile(byte[] data);

        /// <summary>
        /// Извлекает PCM-данные из WAV-файла
        /// </summary>
        /// <param name="wavData">WAV-данные с заголовком</param>
        /// <returns>PCM-данные без заголовка</returns>
        byte[] ExtractPcmFromWav(byte[] wavData);
    }
} 