using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;

namespace SpeechRecognition.Core.Audio
{
    /// <summary>
    /// Класс для обработки аудиоданных
    /// </summary>
    public class AudioProcessor : IAudioProcessor
    {
        private const int DEFAULT_SAMPLE_RATE = 16000;
        private const int DEFAULT_BITS_PER_SAMPLE = 16;
        private const int DEFAULT_CHANNELS = 1;

        /// <summary>
        /// Подготавливает аудиоданные для распознавания
        /// </summary>
        /// <param name="audioData">Исходные аудиоданные (PCM или WAV)</param>
        /// <returns>Подготовленные аудиоданные в формате WAV, готовые для распознавания</returns>
        public async Task<byte[]> PrepareAudioDataAsync(byte[] audioData)
        {
            if (audioData == null || audioData.Length == 0)
            {
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioData));
            }

            // Проверяем, является ли входящий массив данных WAV-файлом
            if (IsWavFormat(audioData))
            {
                // Если данные уже в WAV-формате, проверяем требуется ли ресемплирование
                using (var stream = new MemoryStream(audioData))
                using (var reader = new WaveFileReader(stream))
                {
                    // Whisper требует 16кГц моно
                    if (reader.WaveFormat.SampleRate == DEFAULT_SAMPLE_RATE &&
                        reader.WaveFormat.Channels == DEFAULT_CHANNELS)
                    {
                        // Если формат уже соответствует требованиям, просто возвращаем данные
                        return audioData;
                    }
                    else
                    {
                        // Конвертируем формат
                        return ConvertWavFormat(audioData, DEFAULT_SAMPLE_RATE, DEFAULT_CHANNELS);
                    }
                }
            }
            else
            {
                // Если это не WAV, предполагаем что это PCM-данные и добавляем к ним WAV-заголовок
                return AddWavHeader(audioData);
            }
        }

        /// <summary>
        /// Подготавливает аудиофайл для распознавания
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <returns>Подготовленные аудиоданные в формате WAV, готовые для распознавания</returns>
        public async Task<byte[]> PrepareAudioFileAsync(string audioFilePath)
        {
            if (string.IsNullOrEmpty(audioFilePath))
            {
                throw new ArgumentException("Путь к аудиофайлу не может быть пустым", nameof(audioFilePath));
            }

            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Аудиофайл не найден", audioFilePath);
            }

            byte[] audioData = await File.ReadAllBytesAsync(audioFilePath);
            return await PrepareAudioDataAsync(audioData);
        }

        /// <summary>
        /// Проверяет, является ли массив байтов WAV-файлом
        /// </summary>
        /// <param name="data">Проверяемые данные</param>
        /// <returns>true, если данные имеют заголовок WAV-файла</returns>
        public bool IsWavFormat(byte[] data)
        {
            if (data.Length < 12)
            {
                return false;
            }

            // Проверяем RIFF заголовок
            string riffHeader = Encoding.ASCII.GetString(data, 0, 4);
            string waveHeader = Encoding.ASCII.GetString(data, 8, 4);

            return riffHeader == "RIFF" && waveHeader == "WAVE";
        }

        /// <summary>
        /// Добавляет WAV-заголовок к PCM-данным
        /// </summary>
        /// <param name="pcmData">PCM-данные без заголовка</param>
        /// <param name="sampleRate">Частота дискретизации (по умолчанию 16000 Гц)</param>
        /// <param name="bitsPerSample">Бит на семпл (по умолчанию 16 бит)</param>
        /// <param name="channels">Количество каналов (по умолчанию 1 - моно)</param>
        /// <returns>Данные с WAV-заголовком</returns>
        public byte[] AddWavHeader(byte[] pcmData, int sampleRate = DEFAULT_SAMPLE_RATE, 
                                  int bitsPerSample = DEFAULT_BITS_PER_SAMPLE, 
                                  int channels = DEFAULT_CHANNELS)
        {
            using (var stream = new MemoryStream())
            {
                // Создаем WAV заголовок
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    // RIFF заголовок
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(pcmData.Length + 36); // Размер всего файла - 36 + размер данных
                    writer.Write(Encoding.ASCII.GetBytes("WAVE"));

                    // Формат (fmt) блок
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));
                    writer.Write(16); // Размер формат-блока (16 для PCM)
                    writer.Write((short)1); // PCM = 1
                    writer.Write((short)channels); // Количество каналов
                    writer.Write(sampleRate); // Частота дискретизации
                    writer.Write(sampleRate * channels * bitsPerSample / 8); // Байт в секунду
                    writer.Write((short)(channels * bitsPerSample / 8)); // Байт в семпле
                    writer.Write((short)bitsPerSample); // Бит в семпле

                    // Данные
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(pcmData.Length); // Размер PCM-данных
                    writer.Write(pcmData); // Сами PCM-данные
                }

                return stream.ToArray();
            }
        }

        /// <summary>
        /// Конвертирует WAV-файл в нужный формат
        /// </summary>
        /// <param name="wavData">Исходные WAV-данные</param>
        /// <param name="targetSampleRate">Целевая частота дискретизации</param>
        /// <param name="targetChannels">Целевое количество каналов</param>
        /// <returns>Сконвертированные WAV-данные</returns>
        private byte[] ConvertWavFormat(byte[] wavData, int targetSampleRate, int targetChannels)
        {
            using (var inputStream = new MemoryStream(wavData))
            using (var reader = new WaveFileReader(inputStream))
            using (var outputStream = new MemoryStream())
            {
                // Создаем целевой формат
                var targetFormat = new WaveFormat(targetSampleRate, DEFAULT_BITS_PER_SAMPLE, targetChannels);
                
                using (var resampler = new MediaFoundationResampler(reader, targetFormat))
                {
                    // Установка высокого качества ресемплирования
                    resampler.ResamplerQuality = 60;
                    WaveFileWriter.WriteWavFileToStream(outputStream, resampler);
                }

                return outputStream.ToArray();
            }
        }
    }
} 