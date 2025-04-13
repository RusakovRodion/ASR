using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

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
        private readonly ILogger _logger;

        /// <summary>
        /// Создает новый экземпляр класса AudioProcessor
        /// </summary>
        public AudioProcessor()
        {
            // Создаем NULL логгер, если не предоставлен
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

        /// <summary>
        /// Создает новый экземпляр класса AudioProcessor с логгером
        /// </summary>
        /// <param name="logger">Логгер</param>
        public AudioProcessor(ILogger logger)
        {
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        }

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

            // Для обеспечения асинхронного выполнения метода
            await Task.Yield();

            // Проверяем, является ли входящий массив данных WAV-файлом
            if (IsWavFormat(audioData))
            {
                try
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
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Ошибка при обработке WAV-аудио. Возможно, файл поврежден или имеет неподдерживаемый формат", ex);
                }
            }
            else
            {
                // В соответствии с техническим заданием, система должна поддерживать только WAV формат
                // Если данные не являются WAV, но похожи на PCM, можно попытаться добавить заголовок
                if (audioData.Length > 1000) // Простая эвристика для проверки на PCM-данные
                {
                    try 
                    {
                        _logger.LogWarning("Аудиоданные не в формате WAV. Пытаемся обработать как PCM-данные");
                        return AddWavHeader(audioData);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Аудиоданные не в формате WAV и не могут быть преобразованы в WAV", ex);
                    }
                }
                else
                {
                    throw new InvalidOperationException("Аудиоданные должны быть в формате WAV в соответствии с техническим заданием");
                }
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

            // Асинхронно считываем файл
            byte[] audioData = await File.ReadAllBytesAsync(audioFilePath);
            
            // Подготавливаем аудиоданные
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
        /// Проверяет, является ли массив байтов WAV-файлом
        /// </summary>
        /// <param name="data">Проверяемые данные</param>
        /// <returns>true, если данные имеют заголовок WAV-файла</returns>
        public bool IsWavFile(byte[] data)
        {
            return IsWavFormat(data);
        }
        
        /// <summary>
        /// Извлекает PCM-данные из WAV-файла
        /// </summary>
        /// <param name="wavData">WAV-данные с заголовком</param>
        /// <returns>PCM-данные без заголовка</returns>
        public byte[] ExtractPcmFromWav(byte[] wavData)
        {
            if (!IsWavFile(wavData))
            {
                throw new ArgumentException("Данные не являются WAV-файлом", nameof(wavData));
            }
            
            using (var stream = new MemoryStream(wavData))
            using (var reader = new WaveFileReader(stream))
            {
                // Находим расположение data-секции в WAV-файле
                int dataOffset = -1;
                int dataSize = 0;
                
                // Перенаправляем чтение в самое начало
                stream.Position = 0;
                using (var br = new BinaryReader(stream))
                {
                    // Пропускаем 4 байта "RIFF"
                    br.ReadBytes(4);
                    // Размер файла
                    br.ReadInt32();
                    // Пропускаем 4 байта "WAVE"
                    br.ReadBytes(4);
                    
                    // Ищем секцию "data"
                    while (stream.Position < stream.Length - 8)
                    {
                        string chunkId = Encoding.ASCII.GetString(br.ReadBytes(4));
                        int chunkSize = br.ReadInt32();
                        
                        if (chunkId == "data")
                        {
                            dataOffset = (int)stream.Position;
                            dataSize = chunkSize;
                            break;
                        }
                        
                        // Пропускаем текущую секцию
                        stream.Position += chunkSize;
                    }
                }
                
                if (dataOffset == -1)
                {
                    throw new InvalidOperationException("Не найдена секция data в WAV-файле");
                }
                
                // Копируем только PCM-данные
                byte[] pcmData = new byte[dataSize];
                Array.Copy(wavData, dataOffset, pcmData, 0, dataSize);
                
                return pcmData;
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

        /// <summary>
        /// Разбивает аудиофайл на указанное количество отдельных WAV-фрагментов
        /// </summary>
        /// <param name="filePath">Путь к исходному WAV-файлу</param>
        /// <param name="numChunks">Количество фрагментов для разбиения</param>
        /// <returns>Список байтовых массивов, содержащих независимые WAV-фрагменты</returns>
        public async Task<List<byte[]>> SplitAudioFileIntoChunksAsync(string filePath, int numChunks)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Аудиофайл не найден", filePath);
            }

            if (numChunks <= 0)
            {
                throw new ArgumentException("Количество фрагментов должно быть положительным числом", nameof(numChunks));
            }

            // Чтение файла
            byte[] fileData = await File.ReadAllBytesAsync(filePath);
            
            // Проверяем, что это WAV файл
            if (!IsWavFile(fileData))
            {
                throw new InvalidOperationException("Файл должен быть в формате WAV");
            }
            
            WaveFormat waveFormat;
            using (var stream = new MemoryStream(fileData))
            using (var reader = new WaveFileReader(stream))
            {
                waveFormat = reader.WaveFormat;
            }
            
            // Минимальный размер чанка - 30 секунд аудио
            int bytesPerSample = waveFormat.BitsPerSample / 8;
            int minChunkSize = 30 * waveFormat.SampleRate * bytesPerSample * waveFormat.Channels;
            
            // Если аудиофайл слишком маленький, возвращаем его целиком
            if (fileData.Length < minChunkSize + 44) // 44 - размер WAV-заголовка
            {
                return new List<byte[]> { fileData };
            }
            
            // Анализируем аудио для обнаружения пауз и разбиения на смысловые фрагменты
            List<int> splitPositions = FindSplitPositions(filePath, numChunks);
            
            // Если не удалось найти подходящие позиции для разбиения (или их меньше 2),
            // используем стандартный алгоритм разбиения на равные части
            if (splitPositions.Count < 2)
            {
                // Извлекаем PCM данные из WAV файла
                byte[] pcmData = ExtractPcmFromWav(fileData);
                
                // Рассчитываем размер чанка при заданном количестве
                int estimatedChunkSize = pcmData.Length / numChunks;
                
                // Проверяем, не получится ли чанк меньше 30 секунд
                if (estimatedChunkSize < minChunkSize && pcmData.Length > minChunkSize)
                {
                    // Рассчитываем максимально возможное количество чанков
                    int maxAllowedChunks = Math.Max(1, pcmData.Length / minChunkSize);
                    numChunks = maxAllowedChunks;
                }
                
                // Если аудиофайл не может быть разбит даже на 2 части, возвращаем его целиком
                if (numChunks <= 1)
                {
                    return new List<byte[]> { fileData };
                }
                
                // Делаем размер фрагмента кратным frameSize
                int frameSize = waveFormat.BlockAlign;
                int chunkSize = pcmData.Length / numChunks;
                chunkSize = (chunkSize / frameSize) * frameSize;
                
                List<byte[]> pcmChunks = new List<byte[]>();
                
                // Разбиваем PCM-данные на равные фрагменты
                for (int i = 0; i < numChunks; i++)
                {
                    int startIndex = i * chunkSize;
                    int length = chunkSize;
                    
                    if (i == numChunks - 1)
                    {
                        length = pcmData.Length - startIndex;
                        length = (length / frameSize) * frameSize;
                    }
                    
                    if (startIndex < 0) startIndex = 0;
                    if (startIndex + length > pcmData.Length) length = (pcmData.Length - startIndex) / frameSize * frameSize;
                    
                    if (length <= 0) continue;
                    
                    byte[] chunk = new byte[length];
                    Array.Copy(pcmData, startIndex, chunk, 0, length);
                    
                    pcmChunks.Add(chunk);
                }
                
                // Создаем WAV-фрагменты
                List<byte[]> wavChunks = new List<byte[]>();
                foreach (var pcmChunk in pcmChunks)
                {
                    byte[] wavChunk = AddWavHeader(pcmChunk, waveFormat.SampleRate, waveFormat.BitsPerSample, waveFormat.Channels);
                    wavChunks.Add(wavChunk);
                }
                
                return wavChunks;
            }
            else
            {
                // Разбиваем по обнаруженным паузам
                return SplitAudioAtPositions(filePath, splitPositions);
            }
        }
        
        /// <summary>
        /// Находит оптимальные позиции для разбиения аудиофайла на основе анализа пауз
        /// </summary>
        /// <param name="filePath">Путь к WAV-файлу</param>
        /// <param name="numChunks">Желаемое количество фрагментов</param>
        /// <returns>Список позиций (в отсчетах) для разбиения аудио</returns>
        private List<int> FindSplitPositions(string filePath, int numChunks)
        {
            List<int> splitPositions = new List<int>();
            
            try
            {
                using (var audioFile = new AudioFileReader(filePath))
                {
                    // Добавляем начальную позицию
                    splitPositions.Add(0);
                    
                    int sampleRate = audioFile.WaveFormat.SampleRate;
                    int channels = audioFile.WaveFormat.Channels;
                    int totalSamples = (int)(audioFile.Length / audioFile.WaveFormat.BlockAlign);
                    
                    // Читаем весь файл для анализа
                    float[] allSamples = new float[totalSamples * channels];
                    audioFile.Read(allSamples, 0, allSamples.Length);
                    
                    // Размер окна для анализа в отсчетах (100мс)
                    int windowSize = sampleRate / 10;
                    
                    // Минимальная продолжительность паузы (0.3 секунды)
                    int minPauseSamples = sampleRate / 3;
                    
                    // Порог амплитуды для определения тишины (-40 дБ, примерно 0.01 от полной шкалы)
                    float silenceThreshold = 0.01f;
                    
                    // Список обнаруженных пауз (начало, длительность)
                    List<(int start, int length)> detectedPauses = new List<(int start, int length)>();
                    
                    // Ищем паузы в файле
                    int pauseStart = -1;
                    int pauseLength = 0;
                    
                    for (int i = 0; i < totalSamples; i += windowSize / 2) // Перекрытие окон 50%
                    {
                        // Вычисляем среднюю энергию в текущем окне
                        float energySum = 0;
                        int samplesInWindow = Math.Min(windowSize, totalSamples - i);
                        
                        if (samplesInWindow <= 0) break;
                        
                        for (int j = 0; j < samplesInWindow; j++)
                        {
                            for (int ch = 0; ch < channels; ch++)
                            {
                                int idx = (i + j) * channels + ch;
                                if (idx < allSamples.Length)
                                {
                                    energySum += allSamples[idx] * allSamples[idx];
                                }
                            }
                        }
                        
                        float avgEnergy = energySum / (samplesInWindow * channels);
                        bool isSilence = avgEnergy < silenceThreshold * silenceThreshold;
                        
                        // Обнаружение начала паузы
                        if (isSilence && pauseStart == -1)
                        {
                            pauseStart = i;
                            pauseLength = windowSize / 2;
                        }
                        // Продолжение паузы
                        else if (isSilence && pauseStart != -1)
                        {
                            pauseLength += windowSize / 2;
                        }
                        // Конец паузы
                        else if (!isSilence && pauseStart != -1)
                        {
                            // Сохраняем паузу, если она достаточно длинная
                            if (pauseLength >= minPauseSamples)
                            {
                                detectedPauses.Add((pauseStart, pauseLength));
                            }
                            
                            pauseStart = -1;
                            pauseLength = 0;
                        }
                    }
                    
                    // Проверяем, не заканчивается ли файл паузой
                    if (pauseStart != -1 && pauseLength >= minPauseSamples)
                    {
                        detectedPauses.Add((pauseStart, pauseLength));
                    }
                    
                    // Добавляем конечную позицию
                    splitPositions.Add(totalSamples);
                    
                    _logger.LogInformation($"Обнаружено {detectedPauses.Count} пауз в аудиофайле");
                    
                    if (detectedPauses.Count > 0)
                    {
                        // Создаем массив идеальных позиций для равномерного разделения
                        List<int> idealPositions = new List<int>();
                        int chunkSize = totalSamples / numChunks;
                        
                        for (int i = 1; i < numChunks; i++)
                        {
                            idealPositions.Add(i * chunkSize);
                        }
                        
                        // Максимальное отклонение от идеальной позиции (10% от размера чанка)
                        int maxDeviation = (int)(chunkSize * 0.1);
                        
                        // Создаем список позиций, выбирая паузы наиболее близкие к идеальным позициям
                        List<int> actualPositions = new List<int>();
                        
                        foreach (int idealPos in idealPositions)
                        {
                            // Ищем ближайшую паузу к идеальной позиции
                            int closestPausePos = -1;
                            int minDistance = int.MaxValue;
                            
                            foreach (var pause in detectedPauses)
                            {
                                // Берем середину паузы
                                int pauseMiddle = pause.start + pause.length / 2;
                                int distance = Math.Abs(pauseMiddle - idealPos);
                                
                                // Если эта пауза ближе к идеальной позиции и находится в пределах допустимого отклонения
                                if (distance < minDistance && distance <= maxDeviation)
                                {
                                    minDistance = distance;
                                    closestPausePos = pauseMiddle;
                                }
                            }
                            
                            // Если нашли подходящую паузу, используем ее, иначе используем идеальную позицию
                            if (closestPausePos != -1)
                            {
                                actualPositions.Add(closestPausePos);
                            }
                            else
                            {
                                actualPositions.Add(idealPos);
                            }
                        }
                        
                        // Сортируем позиции по возрастанию
                        actualPositions.Sort();
                        
                        // Создаем окончательный список точек разбиения
                        splitPositions = new List<int> { 0 }; // Начало файла
                        splitPositions.AddRange(actualPositions);
                        splitPositions.Add(totalSamples); // Конец файла
                        
                        _logger.LogInformation($"Файл будет разбит на {splitPositions.Count - 1} фрагментов, оптимизированных по равномерности и паузам");
                    }
                    else
                    {
                        _logger.LogInformation("Паузы не обнаружены, файл будет разбит на равные части");
                        
                        // Делим файл на равные части
                        int chunkSize = totalSamples / numChunks;
                        splitPositions = new List<int> { 0 };
                        
                        for (int i = 1; i < numChunks; i++)
                        {
                            splitPositions.Add(i * chunkSize);
                        }
                        
                        splitPositions.Add(totalSamples);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при анализе пауз");
                splitPositions = new List<int> { 0 };
            }
            
            return splitPositions;
        }
        
        /// <summary>
        /// Разбивает аудиофайл по указанным позициям
        /// </summary>
        /// <param name="filePath">Путь к WAV-файлу</param>
        /// <param name="positions">Список позиций для разбиения (в отсчетах)</param>
        /// <returns>Список фрагментов WAV</returns>
        private List<byte[]> SplitAudioAtPositions(string filePath, List<int> positions)
        {
            List<byte[]> chunks = new List<byte[]>();
            
            try
            {
                if (positions.Count < 2) return chunks;
                
                using (var audioFile = new AudioFileReader(filePath))
                {
                    WaveFormat waveFormat = audioFile.WaveFormat;
                    int bytesPerSample = waveFormat.BitsPerSample / 8;
                    int channels = waveFormat.Channels;
                    int frameSize = waveFormat.BlockAlign;
                    
                    // Проходим по всем парам соседних позиций
                    for (int i = 0; i < positions.Count - 1; i++)
                    {
                        int startPos = positions[i];
                        int endPos = positions[i + 1];
                        
                        // Определяем длину фрагмента в отсчетах
                        int length = endPos - startPos;
                        
                        // Если фрагмент слишком короткий, пропускаем его
                        if (length <= 0 || length < waveFormat.SampleRate) continue;
                        
                        // Создаем буфер для хранения сэмплов аудио
                        float[] sampleBuffer = new float[length * channels];
                        
                        // Устанавливаем позицию для чтения
                        audioFile.Position = startPos * frameSize;
                        
                        // Читаем данные из файла
                        int samplesRead = audioFile.Read(sampleBuffer, 0, sampleBuffer.Length);
                        
                        if (samplesRead > 0)
                        {
                            // Преобразуем в байтовый массив с WAV-заголовком
                            using (var memStream = new MemoryStream())
                            {
                                using (var writer = new WaveFileWriter(memStream, waveFormat))
                                {
                                    writer.WriteSamples(sampleBuffer, 0, samplesRead);
                                }
                                
                                chunks.Add(memStream.ToArray());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Логируем ошибку, чтобы упростить отладку
                _logger.LogError(ex, "Ошибка при разбиении файла");
            }
            
            return chunks;
        }
    }
} 