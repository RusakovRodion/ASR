using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Sessions;

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Класс для потокового распознавания речи
    /// </summary>
    public class WhisperStreamProcessor : IDisposable
    {
        private readonly ILogger _logger;
        private readonly IAudioProcessor _audioProcessor;
        private readonly ISpeechRecognizer _recognizer;
        private readonly SessionManager _sessionManager;
        private readonly ModelDownloader _modelDownloader;
        private RecognitionSession _currentSession;
        private bool _isDisposed;

        /// <summary>
        /// Создает новый экземпляр класса WhisperStreamProcessor
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="modelPath">Путь к файлу модели</param>
        /// <param name="language">Код языка (по умолчанию "ru")</param>
        /// <param name="modelType">Тип модели (tiny, base)</param>
        public WhisperStreamProcessor(ILogger logger, string modelPath, string language = "ru", string modelType = "base")
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));
            }

            _modelDownloader = new ModelDownloader(logger);
            _audioProcessor = new AudioProcessor();
            _recognizer = new WhisperRecognizer(_audioProcessor, modelPath, language);
            _sessionManager = new SessionManager(_recognizer);
            _isDisposed = false;

            // Создаем сессию для обработки потока
            _currentSession = _sessionManager.CreateSession();
            
            _logger.LogInformation($"WhisperStreamProcessor создан с моделью: {modelPath}, язык: {language}");
        }

        /// <summary>
        /// Инициализирует процессор
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        public async Task InitializeAsync()
        {
            ThrowIfDisposed();

            try
            {
                // Проверяем наличие модели и скачиваем её при необходимости
                string modelPath = await _modelDownloader.EnsureModelExistsAsync(
                    ((WhisperRecognizer)_recognizer).ModelPath,
                    Path.GetFileNameWithoutExtension(((WhisperRecognizer)_recognizer).ModelPath).Contains("tiny") ? "tiny" : "base");
                
                // Инициализация распознавателя
                await _recognizer.InitializeAsync();
                
                _logger.LogInformation("WhisperStreamProcessor успешно инициализирован");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при инициализации WhisperStreamProcessor");
                throw;
            }
        }

        /// <summary>
        /// Обрабатывает фрагмент аудиоданных из потока
        /// </summary>
        /// <param name="audioChunk">Фрагмент аудиоданных (PCM или WAV)</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public async Task<string> ProcessStreamAsync(byte[] audioChunk, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (audioChunk == null || audioChunk.Length == 0)
            {
                _logger.LogWarning("Получен пустой аудиофрагмент");
                return string.Empty;
            }

            try
            {
                _logger.LogDebug($"Обработка аудиофрагмента размером {audioChunk.Length} байт");
                var result = await _sessionManager.ProcessFragmentAsync(_currentSession.Id, audioChunk, cancellationToken);
                
                if (result != null)
                {
                    _logger.LogDebug($"Распознан текст: {result.Text}");
                    return result.Text;
                }
                else
                {
                    _logger.LogWarning("Не удалось получить результат распознавания");
                    return string.Empty;
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Операция распознавания была отменена");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке аудиофрагмента");
                throw;
            }
        }

        /// <summary>
        /// Обрабатывает аудиофайл целиком
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public async Task<string> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("Путь к файлу не может быть пустым", nameof(filePath));
            }

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Аудиофайл не найден", filePath);
            }

            try
            {
                _logger.LogInformation($"Обработка файла: {filePath}");
                return await _recognizer.RecognizeSpeechFromFileAsync(filePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Операция распознавания файла была отменена");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при обработке файла: {filePath}");
                throw;
            }
        }

        /// <summary>
        /// Создает новую сессию распознавания
        /// </summary>
        /// <returns>Идентификатор созданной сессии</returns>
        public Guid CreateNewSession()
        {
            ThrowIfDisposed();

            _currentSession = _sessionManager.CreateSession();
            _logger.LogInformation($"Создана новая сессия с ID: {_currentSession.Id}");
            return _currentSession.Id;
        }

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _sessionManager.Dispose();
            _recognizer.Dispose();
            
            _isDisposed = true;
            _logger.LogInformation("WhisperStreamProcessor освобожден");
            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(WhisperStreamProcessor));
            }
        }
    }
} 