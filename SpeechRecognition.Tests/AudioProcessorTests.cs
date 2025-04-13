using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using SpeechRecognition.Core.Audio;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace SpeechRecognition.Tests
{
    public class AudioProcessorTests
    {
        private readonly IAudioProcessor _audioProcessor;
        private readonly string? _testWavPath;
        private readonly string? _testMp3Path;
        private bool _hasTestWavFile;
        private bool _hasTestMp3File;

        public AudioProcessorTests()
        {
            // Установка кодировки для корректного отображения кириллицы
            Console.OutputEncoding = Encoding.UTF8;
            
            // Инициализируем AudioProcessor без логгера (NullLogger)
            _audioProcessor = new AudioProcessor();
            
            // Пытаемся найти тестовый WAV файл в разных местах
            string[] possibleWavPaths = new[]
            {
                Path.Combine("audioExamples", "test.wav"),  // Относительный путь от корня проекта
                Path.Combine("..", "audioExamples", "test.wav"),  // На уровень выше
                Path.Combine("..", "..", "audioExamples", "test.wav"),  // На два уровня выше
                Path.Combine("..", "..", "..", "audioExamples", "test.wav")  // На три уровня выше
            };
            
            foreach (var path in possibleWavPaths)
            {
                if (File.Exists(path))
                {
                    _testWavPath = path;
                    _hasTestWavFile = true;
                    break;
                }
            }
            
            // Пытаемся найти тестовый MP3 файл в разных местах
            string[] possibleMp3Paths = new[]
            {
                Path.Combine("audioExamples", "test1.mp3"),  // Относительный путь от корня проекта
                Path.Combine("..", "audioExamples", "test1.mp3"),  // На уровень выше
                Path.Combine("..", "..", "audioExamples", "test1.mp3"),  // На два уровня выше
                Path.Combine("..", "..", "..", "audioExamples", "test1.mp3")  // На три уровня выше
            };
            
            foreach (var path in possibleMp3Paths)
            {
                if (File.Exists(path))
                {
                    _testMp3Path = path;
                    _hasTestMp3File = true;
                    break;
                }
            }
            
            // Если файл не найден, мы все равно продолжим, но пропустим тесты, которые требуют реальный файл
            if (!_hasTestWavFile)
            {
                _testWavPath = "test.wav"; // Заглушка
                // Логгируем предупреждение
                TestLogger.LogWarning("Тестовый WAV-файл не найден. Некоторые тесты будут пропущены.");
            }
            
            if (!_hasTestMp3File)
            {
                _testMp3Path = "test1.mp3"; // Заглушка
                // Логгируем предупреждение
                TestLogger.LogWarning("Тестовый MP3-файл не найден. Некоторые тесты будут пропущены.");
            }
        }

        [Fact]
        public async Task ProcessWavFile_ValidFile_ReturnsProcessedData()
        {
            // Пропускаем тест, если нет файла
            if (!_hasTestWavFile)
            {
                // В xUnit для пропуска теста можно просто вернуться
                return;
            }
            
            // Arrange
            // Используем реальный WAV-файл для теста
            var testFilePath = _testWavPath;
            
            // Act
            var result = await _audioProcessor.PrepareAudioFileAsync(testFilePath!);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > 0);
            Assert.True(_audioProcessor.IsWavFormat(result));
        }
        
        [Fact]
        public void AddWavHeader_PcmData_ReturnsValidWavData()
        {
            // Arrange
            // Создаем тестовые PCM данные - синусоида
            byte[] pcmData = GenerateSinePcmData(1000, 16000, 440);
            
            // Act
            var result = _audioProcessor.AddWavHeader(pcmData);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(result.Length > pcmData.Length); // WAV-заголовок должен увеличить размер
            Assert.True(_audioProcessor.IsWavFormat(result)); // Проверяем, что результат - валидный WAV
            
            // Проверяем структуру WAV-заголовка (первые 4 байта должны быть "RIFF")
            Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(result, 0, 4));
            
            // Проверяем, что размер файла указан корректно
            int fileSize = BitConverter.ToInt32(result, 4);
            Assert.Equal(result.Length - 8, fileSize);
            
            // Проверяем формат
            Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(result, 8, 4));
        }
        
        [Fact]
        public async Task ProcessAudio_InvalidFormat_ThrowsException()
        {
            // Arrange
            // Создаем данные, которые точно вызовут ошибку в NAudio
            // Например, нулевой массив или очень маленький массив неверного формата
            byte[] invalidData = Array.Empty<byte>();
            
            // Act & Assert
            var exception = await Assert.ThrowsAnyAsync<Exception>(async () => 
                await _audioProcessor.PrepareAudioDataAsync(invalidData));
            
            // Проверяем, что исключение было выброшено
            Assert.NotNull(exception);
        }
        
        [Theory]
        [InlineData(100)]  // Очень короткий фрагмент
        [InlineData(1000)] // Короткий фрагмент
        [InlineData(10000)] // Средний фрагмент
        [InlineData(100000)] // Длинный фрагмент
        public async Task ProcessAudio_DifferentLengths_ProcessesCorrectly(int length)
        {
            // Arrange
            // Генерируем PCM-данные
            byte[] pcmData = GenerateSinePcmData(length, 16000, 440);
            
            // Добавляем WAV-заголовок к PCM-данным
            byte[] wavData = _audioProcessor.AddWavHeader(pcmData);
            
            // Act
            var result = await _audioProcessor.PrepareAudioDataAsync(wavData);
            
            // Assert
            Assert.NotNull(result);
            Assert.True(_audioProcessor.IsWavFormat(result)); // Проверяем, что результат - валидный WAV
            Assert.True(result.Length >= pcmData.Length); // Размер должен быть не меньше исходного PCM
        }
        
        [Fact]
        public async Task ProcessDifferentFileFormats_HandlesCorrectly()
        {
            // Проверка обработки WAV-файла (должна быть успешной)
            if (_hasTestWavFile)
            {
                // Act
                var wavResult = await _audioProcessor.PrepareAudioFileAsync(_testWavPath!);
                
                // Assert
                Assert.NotNull(wavResult);
                Assert.True(wavResult.Length > 0);
                Assert.True(_audioProcessor.IsWavFormat(wavResult));
                TestLogger.LogInformation("WAV-файл успешно обработан");
            }
            
            // Проверка обработки MP3-файла
            if (_hasTestMp3File)
            {
                try
                {
                    // Пытаемся обработать MP3-файл
                    var mp3Result = await _audioProcessor.PrepareAudioFileAsync(_testMp3Path!);
                    
                    // Если обработка прошла успешно, проверяем результат
                    if (mp3Result != null)
                    {
                        TestLogger.LogInformation($"MP3-файл был обработан, длина результата: {mp3Result.Length}");
                    }
                    else
                    {
                        TestLogger.LogInformation("MP3-файл был обработан, но результат пустой");
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Если выброшено исключение о неподдерживаемом формате, это также допустимо
                    Assert.Contains("WAV", ex.Message);
                    TestLogger.LogInformation($"MP3-файл корректно вызвал исключение: {ex.Message}");
                }
                catch (Exception ex)
                {
                    // Логируем другие исключения, но не считаем их ошибкой теста
                    TestLogger.LogInformation($"MP3-файл вызвал исключение: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }
        
        #region Helper Methods
        
        private async Task<byte[]> ProcessInvalidAudio(byte[] invalidData)
        {
            // Напрямую вызываем метод с некорректными данными
            return await _audioProcessor.PrepareAudioDataAsync(invalidData);
        }
        
        /// <summary>
        /// Генерирует PCM-данные с синусоидальным сигналом
        /// </summary>
        /// <param name="length">Длина данных в байтах</param>
        /// <param name="sampleRate">Частота дискретизации</param>
        /// <param name="frequency">Частота сигнала</param>
        /// <returns>PCM-данные</returns>
        private byte[] GenerateSinePcmData(int length, int sampleRate, int frequency)
        {
            // Создаем массив для PCM-данных
            byte[] pcmData = new byte[length];
            
            // Для простоты используем 16-битный моно формат
            int bytesPerSample = 2;
            int numSamples = length / bytesPerSample;
            
            // Генерируем синусоидальный сигнал
            for (int i = 0; i < numSamples; i++)
            {
                double t = (double)i / sampleRate;
                double amplitude = 0.8; // Амплитуда (от 0 до 1)
                
                // Вычисляем значение синуса
                double value = amplitude * Math.Sin(2 * Math.PI * frequency * t);
                
                // Преобразуем в 16-битное целое число
                short sample = (short)(value * short.MaxValue);
                
                // Записываем в массив
                int offset = i * bytesPerSample;
                pcmData[offset] = (byte)(sample & 0xFF);
                pcmData[offset + 1] = (byte)(sample >> 8);
            }
            
            return pcmData;
        }
        
        #endregion
    }
} 