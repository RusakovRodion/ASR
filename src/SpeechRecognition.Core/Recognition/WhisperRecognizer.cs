using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Audio;
using Whisper.net;
using Whisper.net.Ggml;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Класс для распознавания речи с использованием модели Whisper
    /// </summary>
    public class WhisperRecognizer : ISpeechRecognizer
    {
        private readonly IAudioProcessor _audioProcessor;
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _whisperProcessor;
        private readonly string _language;
        private bool _isInitialized;
        private bool _isDisposed;

        /// <summary>
        /// Путь к файлу модели
        /// </summary>
        public string ModelPath { get; }

        /// <summary>
        /// Создает новый экземпляр класса WhisperRecognizer
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио для подготовки данных</param>
        /// <param name="modelPath">Путь к файлу модели Whisper</param>
        /// <param name="language">Код языка (по умолчанию "ru" - русский)</param>
        public WhisperRecognizer(IAudioProcessor audioProcessor, string modelPath, string language = "ru")
        {
            _audioProcessor = audioProcessor ?? throw new ArgumentNullException(nameof(audioProcessor));
            
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));
            }

            ModelPath = modelPath;
            _language = string.IsNullOrEmpty(language) ? "ru" : language;
            _isInitialized = false;
            _isDisposed = false;
        }

        /// <summary>
        /// Инициализирует распознаватель речи, загружая модель Whisper
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            ThrowIfDisposed();

            if (!File.Exists(ModelPath))
            {
                throw new FileNotFoundException("Файл модели не найден", ModelPath);
            }

            try
            {
                _whisperFactory = WhisperFactory.FromPath(ModelPath);
                _whisperProcessor = _whisperFactory.CreateBuilder()
                    .WithLanguage(_language)
                    .Build();

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Ошибка инициализации модели Whisper", ex);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Распознает речь из аудиоданных
        /// </summary>
        /// <param name="audioData">Аудиоданные в формате WAV</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public async Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
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
                // Подготавливаем аудиоданные (убеждаемся, что они в формате WAV с нужными параметрами)
                byte[] preparedAudioData = await _audioProcessor.PrepareAudioDataAsync(audioData);

                using (var memoryStream = new MemoryStream(preparedAudioData))
                {
                    var result = new System.Text.StringBuilder();

                    // Выполняем распознавание
                    await foreach (var segment in _whisperProcessor!.ProcessAsync(memoryStream, cancellationToken))
                    {
                        result.Append(segment.Text);
                    }

                    return result.ToString();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Ошибка при распознавании речи", ex);
            }
        }

        /// <summary>
        /// Распознает речь из аудиофайла
        /// </summary>
        /// <param name="audioFilePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public async Task<string> RecognizeSpeechFromFileAsync(string audioFilePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(audioFilePath))
            {
                throw new ArgumentException("Путь к аудиофайлу не может быть пустым", nameof(audioFilePath));
            }

            if (!File.Exists(audioFilePath))
            {
                throw new FileNotFoundException("Аудиофайл не найден", audioFilePath);
            }

            // Подготавливаем аудиоданные из файла
            byte[] audioData = await _audioProcessor.PrepareAudioFileAsync(audioFilePath);
            
            // Распознаем речь
            return await RecognizeSpeechAsync(audioData, cancellationToken);
        }

        /// <summary>
        /// Освобождает ресурсы модели
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _whisperProcessor?.Dispose();
            _whisperFactory?.Dispose();
            
            _isDisposed = true;
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(WhisperRecognizer));
            }
        }
    }
} 