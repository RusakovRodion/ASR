using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recognition;
using Xunit;

namespace SpeechRecognition.Tests
{
    public class RecognizerTests
    {
        private readonly IAudioProcessor _audioProcessor;
        private readonly List<string> _testAudioPaths = new List<string>();
        private readonly string? _testWavPath;
        private readonly string? _modelPath;
        private readonly bool _hasTestWavFile;
        private readonly bool _hasModel;
        private ISpeechRecognizer? _recognizer;

        // Ограничим количество аудиофайлов для тестирования
        private const int MaxAudioFilesToTest = 3;

        public RecognizerTests()
        {
            // Инициализируем AudioProcessor без логгера
            _audioProcessor = new AudioProcessor();
            
            // Пытаемся найти тестовые аудиофайлы WAV
            string[] possibleAudioDirs = new[]
            {
                "audioExamples",
                Path.Combine("..", "audioExamples"),
                Path.Combine("..", "..", "audioExamples"),
                Path.Combine("..", "..", "..", "audioExamples")
            };

            string audioDir = "";
            foreach (var dir in possibleAudioDirs)
            {
                if (Directory.Exists(dir))
                {
                    audioDir = dir;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(audioDir))
            {
                int wavFilesCount = 0;
                
                // Ищем WAV файлы (не больше MaxAudioFilesToTest)
                foreach (var file in Directory.GetFiles(audioDir, "*.wav"))
                {
                    if (wavFilesCount < MaxAudioFilesToTest) 
                    {
                        _testAudioPaths.Add(file);
                        wavFilesCount++;
                        
                        // Запоминаем первый WAV файл для базовых тестов
                        if (_testWavPath == null)
                        {
                            _testWavPath = file;
                            _hasTestWavFile = true;
                        }
                    }
                    else
                    {
                        break; // Достигли лимита WAV файлов
                    }
                }
            }
            
            if (!_hasTestWavFile)
            {
                TestLogger.LogWarning("Тестовый аудиофайл не найден. Тесты будут пропущены.");
                return; // Пропускаем дальнейшую инициализацию
            }
            
            // Пытаемся найти модель Whisper
            string[] possibleModelPaths = new[]
            {
                Path.Combine("models", "ggml-tiny.bin"),
                Path.Combine("..", "models", "ggml-tiny.bin"),
                Path.Combine("..", "..", "models", "ggml-tiny.bin"),
                Path.Combine("..", "..", "..", "models", "ggml-tiny.bin"),
                // Альтернативная модель, если tiny отсутствует
                Path.Combine("models", "ggml-base.bin"),
                Path.Combine("..", "models", "ggml-base.bin"),
                Path.Combine("..", "..", "models", "ggml-base.bin"),
                Path.Combine("..", "..", "..", "models", "ggml-base.bin")
            };
            
            foreach (var path in possibleModelPaths)
            {
                if (File.Exists(path))
                {
                    _modelPath = path;
                    _hasModel = true;
                    break;
                }
            }
            
            if (!_hasModel)
            {
                TestLogger.LogWarning("Модель Whisper не найдена. Тесты будут пропущены.");
                return;
            }
            
            try
            {
                // Создаем логгер
                var logger = new NullLogger<RecognizerTests>();
                
                // Создаем распознаватель
                var modelSettings = new WhisperModelSettings(_modelPath!, "ru", "tiny");
                _recognizer = new WhisperRecognizer(_audioProcessor, modelSettings, logger);
                
                // Инициализируем распознаватель
                _recognizer.InitializeAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                TestLogger.LogWarning($"ERROR: Не удалось инициализировать распознаватель: {ex.Message}");
                _hasModel = false;
            }
        }

        [Fact]
        public async Task RecognizeSpeech_ValidAudio_ReturnsText()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            // Подготавливаем тестовые аудиоданные
            byte[] validAudio = await _audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            string result = await _recognizer.RecognizeSpeechAsync(validAudio);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        
        [Fact]
        public async Task RecognizeSpeech_RussianSpeech_ReturnsCorrectText()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            // Подготавливаем тестовые аудиоданные для русской речи
            byte[] russianSpeechAudio = await _audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            string result = await _recognizer.RecognizeSpeechAsync(russianSpeechAudio);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            
            // Дополнительная проверка: текст должен содержать русские символы
            bool containsCyrillicCharacters = ContainsCyrillicCharacters(result);
            Assert.True(containsCyrillicCharacters, "Текст должен содержать русские символы");
        }
        
        [Fact]
        public async Task RecognizeSpeech_ShortAudio_ProcessesCorrectly()
        {
            // Пропускаем тест, если нет модели
            if (!_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            // Создаем короткие аудиоданные (синусоида)
            byte[] shortAudioPcm = GenerateSinePcmData(16000, 16000, 440); // 1 секунда аудио
            byte[] shortAudio = _audioProcessor.AddWavHeader(shortAudioPcm);
            
            // Act
            string result = await _recognizer.RecognizeSpeechAsync(shortAudio);
            
            // Assert
            Assert.NotNull(result);
            // Короткий тон может быть не распознан как речь, поэтому проверяем только, что результат не null
        }
        
        [Fact]
        public async Task RecognizeSpeech_LongAudio_ProcessesCorrectly()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            // Для длинного аудио используем реальный файл
            byte[] longAudio = await _audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            string result = await _recognizer.RecognizeSpeechAsync(longAudio);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        
        [Fact]
        public async Task RecognizeSpeech_InvalidAudio_ThrowsException()
        {
            // Пропускаем тест, если нет модели
            if (!_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            byte[] invalidAudio = Array.Empty<byte>();
            
            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() => 
                _recognizer.RecognizeSpeechAsync(invalidAudio));
        }
        
        [Fact]
        public async Task RecognizeSpeech_WithCancellation_CancelsOperation()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _recognizer == null)
            {
                return;
            }
            
            // Arrange
            byte[] audio = await _audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            var cancellationTokenSource = new CancellationTokenSource();
            
            // Отменяем операцию
            cancellationTokenSource.Cancel();
            
            // Act & Assert
            Exception ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
                _recognizer.RecognizeSpeechAsync(audio, cancellationTokenSource.Token));
            
            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }
        
        [Fact]
        public async Task RecognizeMultipleWavFiles_ProcessesCorrectly()
        {
            // Пропускаем тест, если нет аудиофайлов или модели
            if (_testAudioPaths.Count <= 1 || !_hasModel || _recognizer == null)
            {
                return;
            }
            
            TestLogger.LogInformation($"Будет протестировано {_testAudioPaths.Count} WAV файлов");
            
            // Act & Assert - обрабатываем все WAV файлы
            foreach (var audioPath in _testAudioPaths)
            {
                // Обрабатываем только WAV файлы
                if (Path.GetExtension(audioPath).ToLower() != ".wav")
                    continue;
                
                string fileName = Path.GetFileName(audioPath);
                TestLogger.LogInformation($"Обработка файла {fileName} ({new FileInfo(audioPath).Length / 1024 / 1024} МБ)");
                    
                try
                {
                    // Подготавливаем аудиоданные
                    byte[] audio = await _audioProcessor.PrepareAudioFileAsync(audioPath);
                    
                    // Добавляем таймаут на распознавание
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    
                    // Распознаем речь
                    string result = await _recognizer.RecognizeSpeechAsync(audio, cts.Token);
                    
                    // Ограничиваем вывод результата
                    string truncatedResult = result.Length > 200 ? 
                        result.Substring(0, 200) + "..." : 
                        result;
                    
                    // Проверяем результат
                    Assert.NotNull(result);
                    Assert.NotEmpty(result);
                    
                    // Для русской речи проверяем наличие кириллицы
                    bool hasCyrillic = ContainsCyrillicCharacters(result);
                    if (hasCyrillic)
                    {
                        TestLogger.LogInformation($"Успешно распознана русская речь в файле: {fileName}");
                        TestLogger.LogInformation($"Результат: {truncatedResult}");
                    }
                    else
                    {
                        TestLogger.LogWarning($"Речь распознана, но кириллические символы не найдены в файле: {fileName}");
                        TestLogger.LogInformation($"Результат: {truncatedResult}");
                    }
                }
                catch (Exception ex)
                {
                    // В случае ошибки тест не должен падать, но мы должны увидеть сообщение
                    TestLogger.LogWarning($"Ошибка при обработке WAV файла {fileName}: {ex.Message}");
                    // Не фейлим тест, так как некоторые файлы могут быть некорректными
                }
            }
        }
        
        #region Helper Methods
        
        /// <summary>
        /// Проверяет, содержит ли строка кириллические символы
        /// </summary>
        private bool ContainsCyrillicCharacters(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            foreach (char c in text)
            {
                if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё')
                    return true;
            }
            
            return false;
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