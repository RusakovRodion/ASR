using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recognition;
using Moq;
using Xunit;

namespace SpeechRecognition.Tests
{
    /// <summary>
    /// Тесты для проверки механизмов расширения системы и поддержки новых моделей распознавания речи
    /// </summary>
    public class ModelExtensionTests
    {
        private readonly ILogger _logger;
        private readonly IAudioProcessor _audioProcessor;

        public ModelExtensionTests()
        {
            _logger = new NullLogger<ModelExtensionTests>();
            _audioProcessor = new AudioProcessor();
        }

        [Fact]
        public void CustomModelSettings_StoreParameters_Correctly()
        {
            // Arrange
            var parameters = new Dictionary<string, string>
            {
                ["useGPU"] = "true",
                ["beamSize"] = "5",
                ["customParam"] = "value"
            };

            // Act
            var settings = new CustomModelSettings(
                "path/to/model.bin",
                "ru",
                parameters
            );

            // Assert
            Assert.Equal("path/to/model.bin", settings.ModelPath);
            Assert.Equal("ru", settings.Language);
            Assert.Equal(RecognizerType.Custom, settings.RecognizerType);
            Assert.Equal(3, settings.AdditionalParameters.Count);
            Assert.Equal("true", settings.AdditionalParameters["useGPU"]);
            Assert.Equal("5", settings.AdditionalParameters["beamSize"]);
            Assert.Equal("value", settings.AdditionalParameters["customParam"]);
        }

        [Fact]
        public void CustomModelProvider_IsCompatible_WithCustomModelSettings()
        {
            // Arrange
            var provider = new CustomModelProvider(_logger);
            var customSettings = new CustomModelSettings("path/to/model.bin", "ru");
            var whisperSettings = new WhisperModelSettings("path/to/whisper.bin", "ru", "tiny");

            // Act & Assert
            Assert.True(provider.IsCompatible(customSettings));
            Assert.False(provider.IsCompatible(whisperSettings));
        }

        [Fact]
        public async Task CustomModelProvider_EnsureModelExists_ChecksFilePath()
        {
            // Arrange
            var provider = new CustomModelProvider(_logger);
            var tempFilePath = Path.GetTempFileName();
            var settings = new CustomModelSettings(tempFilePath, "ru");

            try
            {
                // Act
                var modelPath = await provider.EnsureModelExistsAsync(settings);

                // Assert
                Assert.Equal(tempFilePath, modelPath);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }

        [Fact]
        public async Task CustomModelProvider_EnsureModelExists_ThrowsIfFileNotFound()
        {
            // Arrange
            var provider = new CustomModelProvider(_logger);
            var nonExistingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var settings = new CustomModelSettings(nonExistingPath, "ru");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() =>
                provider.EnsureModelExistsAsync(settings));
        }

        [Fact]
        public void MyModelSettings_StoresProperties_Correctly()
        {
            // Arrange & Act
            var settings = new MyModelSettings(
                "path/to/my_model.bin",
                "ru",
                useGPU: true,
                beamSize: 5
            );

            // Assert
            Assert.Equal("path/to/my_model.bin", settings.ModelPath);
            Assert.Equal("ru", settings.Language);
            Assert.Equal(RecognizerType.Custom, settings.RecognizerType);
            Assert.True(settings.UseGPU);
            Assert.Equal(5, settings.BeamSize);
        }

        [Fact]
        public void MyModelProvider_IsCompatible_WithMyModelSettings()
        {
            // Arrange
            var provider = new MyModelProvider(_logger);
            var mySettings = new MyModelSettings("path/to/my_model.bin", "ru");
            var customSettings = new CustomModelSettings("path/to/custom.bin", "ru");

            // Act & Assert
            Assert.True(provider.IsCompatible(mySettings));
            Assert.False(provider.IsCompatible(customSettings));
        }

        [Fact]
        public async Task MyModelRecognizer_InitializeAndRecognize_UsesMyModelSettings()
        {
            // Arrange
            var myModelMock = new Mock<IMyModelImplementation>();
            var settings = new MyModelSettings("path/to/my_model.bin", "ru", true, 5);
            
            myModelMock.Setup(m => m.SetLanguage("ru")).Verifiable();
            myModelMock.Setup(m => m.InitializeAsync()).Returns(Task.CompletedTask).Verifiable();
            myModelMock.SetupSet(m => m.UseGPU = true).Verifiable();
            myModelMock.SetupSet(m => m.BeamSize = 5).Verifiable();
            myModelMock.Setup(m => m.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Тестовый результат").Verifiable();

            // Имитируем AudioProcessor, который возвращает корректный WAV формат
            var audioProcessorMock = new Mock<IAudioProcessor>();
            audioProcessorMock.Setup(a => a.IsWavFile(It.IsAny<byte[]>())).Returns(true);
            audioProcessorMock.Setup(a => a.PrepareAudioDataAsync(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[] data) => data);

            var recognizer = new MyModelRecognizer(
                audioProcessorMock.Object,
                settings,
                _logger,
                myModelMock.Object
            );

            // Создаем тестовые аудиоданные с WAV-заголовком
            byte[] audioData = new byte[44 + 1000]; // 44 байта заголовка + 1000 байт данных
            // RIFF заголовок
            audioData[0] = (byte)'R';
            audioData[1] = (byte)'I';
            audioData[2] = (byte)'F';
            audioData[3] = (byte)'F';
            // Размер файла
            audioData[4] = (byte)(1044 & 0xff);
            audioData[5] = (byte)((1044 >> 8) & 0xff);
            audioData[6] = (byte)((1044 >> 16) & 0xff);
            audioData[7] = (byte)((1044 >> 24) & 0xff);
            // WAVE заголовок
            audioData[8] = (byte)'W';
            audioData[9] = (byte)'A';
            audioData[10] = (byte)'V';
            audioData[11] = (byte)'E';

            // Act
            await recognizer.InitializeAsync();
            var result = await recognizer.RecognizeSpeechAsync(audioData);

            // Assert
            Assert.Equal("Тестовый результат", result);
            myModelMock.Verify(m => m.SetLanguage("ru"), Times.Once);
            myModelMock.Verify(m => m.InitializeAsync(), Times.Once);
            myModelMock.VerifySet(m => m.UseGPU = true, Times.Once);
            myModelMock.VerifySet(m => m.BeamSize = 5, Times.Once);
            myModelMock.Verify(m => m.RecognizeAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public void MyModelRecognizer_Dispose_ReleasesResources()
        {
            // Arrange
            var myModelMock = new Mock<IMyModelImplementation>();
            var settings = new MyModelSettings("path/to/my_model.bin", "ru");
            var recognizer = new MyModelRecognizer(
                _audioProcessor,
                settings,
                _logger,
                myModelMock.Object
            );

            // Act
            recognizer.Dispose();

            // Assert
            myModelMock.Verify(m => m.Dispose(), Times.Once);
        }

        #region Внутренние классы для тестирования

        /// <summary>
        /// Тестовый интерфейс для имитации внешней реализации модели
        /// </summary>
        public interface IMyModelImplementation : IDisposable
        {
            bool UseGPU { get; set; }
            int BeamSize { get; set; }
            void SetLanguage(string language);
            Task InitializeAsync();
            Task<string> RecognizeAsync(Stream audioStream, CancellationToken cancellationToken);
        }

        /// <summary>
        /// Пример пользовательских настроек модели распознавания
        /// </summary>
        public class MyModelSettings : IModelSettings
        {
            public RecognizerType RecognizerType => RecognizerType.Custom;
            public string ModelPath { get; }
            public string Language { get; }
            public bool UseGPU { get; }
            public int BeamSize { get; }

            public MyModelSettings(
                string modelPath,
                string language = "ru",
                bool useGPU = false,
                int beamSize = 5)
            {
                ModelPath = modelPath ?? throw new ArgumentNullException(nameof(modelPath));
                Language = language ?? "ru";
                UseGPU = useGPU;
                BeamSize = beamSize;
            }
        }

        /// <summary>
        /// Пример провайдера пользовательской модели
        /// </summary>
        public class MyModelProvider : IModelProvider
        {
            private readonly ILogger _logger;

            public RecognizerType RecognizerType => RecognizerType.Custom;

            public MyModelProvider(ILogger logger)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            }

            public async Task<string> EnsureModelExistsAsync(IModelSettings modelSettings)
            {
                if (!IsCompatible(modelSettings))
                {
                    throw new ArgumentException("Несовместимые настройки модели", nameof(modelSettings));
                }

                var settings = (MyModelSettings)modelSettings;

                if (File.Exists(settings.ModelPath))
                {
                    _logger.LogInformation($"Модель найдена: {settings.ModelPath}");
                    return settings.ModelPath;
                }

                throw new FileNotFoundException("Модель не найдена", settings.ModelPath);
            }

            public bool IsCompatible(IModelSettings modelSettings)
            {
                return modelSettings is MyModelSettings;
            }
        }

        /// <summary>
        /// Пример реализации пользовательской модели распознавания
        /// </summary>
        public class MyModelRecognizer : BaseSpeechRecognizer
        {
            private readonly ILogger _logger;
            private readonly MyModelSettings _settings;
            private readonly IMyModelImplementation _model;
            
            public override RecognizerType RecognizerType => RecognizerType.Custom;

            public MyModelRecognizer(
                IAudioProcessor audioProcessor,
                IModelSettings modelSettings,
                ILogger logger,
                IMyModelImplementation? model = null) 
                : base(audioProcessor, modelSettings)
            {
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                
                if (modelSettings is not MyModelSettings mySettings)
                {
                    throw new ArgumentException("Требуются настройки MyModelSettings", nameof(modelSettings));
                }
                
                _settings = mySettings;
                _model = model ?? throw new ArgumentNullException(nameof(model));
            }

            public override async Task InitializeAsync()
            {
                if (_isInitialized)
                {
                    return;
                }

                ThrowIfDisposed();

                try
                {
                    // Инициализация модели
                    _model.UseGPU = _settings.UseGPU;
                    _model.BeamSize = _settings.BeamSize;
                    _model.SetLanguage(_settings.Language);
                    
                    await _model.InitializeAsync();
                    
                    _isInitialized = true;
                    _logger.LogInformation("Модель успешно инициализирована");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка инициализации модели");
                    throw new InvalidOperationException("Ошибка инициализации модели", ex);
                }
            }

            public override async Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
            {
                ThrowIfDisposed();

                if (!_isInitialized)
                {
                    await InitializeAsync();
                }

                if (audioData == null || audioData.Length == 0)
                {
                    throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioData));
                }

                try
                {
                    // Подготавливаем аудиоданные
                    byte[] preparedAudioData = await _audioProcessor.PrepareAudioDataAsync(audioData);
                    
                    using (var stream = new MemoryStream(preparedAudioData))
                    {
                        // Выполняем распознавание с моделью
                        string result = await _model.RecognizeAsync(stream, cancellationToken);
                        return result;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Ошибка при распознавании речи");
                    throw new InvalidOperationException("Ошибка при распознавании речи", ex);
                }
            }

            public override void Dispose()
            {
                if (_isDisposed)
                {
                    return;
                }

                _model?.Dispose();
                
                _isDisposed = true;
                GC.SuppressFinalize(this);
            }
        }

        #endregion
    }
} 