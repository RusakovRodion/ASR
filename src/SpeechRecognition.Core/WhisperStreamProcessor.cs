using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Sessions;
using System.Collections.Generic;
using System.Text;
using SpeechRecognition.Core.Events;

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
        /// Событие, возникающее при получении результата распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionCompleted;

        /// <summary>
        /// Событие, возникающее перед началом распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionStarted;

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
            
            // Подписываемся на события SessionManager
            _sessionManager.RecognitionStarted += OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted += OnSessionManagerRecognitionCompleted;
            
            _logger.LogInformation($"WhisperStreamProcessor создан с моделью: {modelPath}, язык: {language}");
        }

        // Обработчики событий SessionManager
        private void OnSessionManagerRecognitionStarted(object sender, RecognitionEventArgs e)
        {
            RecognitionStarted?.Invoke(this, e);
        }

        private void OnSessionManagerRecognitionCompleted(object sender, RecognitionEventArgs e)
        {
            RecognitionCompleted?.Invoke(this, e);
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
        /// Обрабатывает аудиофайл с разбиением на указанное количество фрагментов
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу</param>
        /// <param name="numChunks">Количество фрагментов</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        public async Task<string> ProcessFileInChunksAsync(string filePath, int numChunks, CancellationToken cancellationToken = default)
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

            if (numChunks <= 0)
            {
                throw new ArgumentException("Количество фрагментов должно быть положительным числом", nameof(numChunks));
            }

            // Если указан 1 фрагмент, обрабатываем файл целиком
            if (numChunks == 1)
            {
                return await ProcessFileAsync(filePath, cancellationToken);
            }

            try
            {
                _logger.LogInformation($"Обработка файла {filePath} с разбиением на {numChunks} фрагментов");
                
                // Чтение всего файла
                byte[] fileData = await File.ReadAllBytesAsync(filePath, cancellationToken);
                
                // Проверяем, что это WAV файл
                if (!_audioProcessor.IsWavFile(fileData))
                {
                    throw new InvalidOperationException("Файл должен быть в формате WAV");
                }
                
                // Извлекаем PCM данные из WAV файла
                byte[] pcmData = _audioProcessor.ExtractPcmFromWav(fileData);
                
                // Разбиваем на фрагменты
                List<byte[]> chunks = SplitIntoChunks(pcmData, numChunks);
                
                // Создаем новую сессию для обработки фрагментов
                Guid sessionId = CreateNewSession();
                
                StringBuilder resultBuilder = new StringBuilder();
                
                // Обрабатываем каждый фрагмент
                for (int i = 0; i < chunks.Count; i++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new OperationCanceledException("Операция отменена пользователем");
                    }
                    
                    _logger.LogInformation($"Обработка фрагмента {i + 1} из {chunks.Count}");
                    
                    // Добавляем WAV-заголовок к PCM данным
                    byte[] wavChunk = _audioProcessor.AddWavHeader(chunks[i]);
                    
                    // Обрабатываем фрагмент
                    var result = await _sessionManager.ProcessFragmentAsync(sessionId, wavChunk, cancellationToken);
                    
                    if (result != null && !string.IsNullOrEmpty(result.Text))
                    {
                        if (resultBuilder.Length > 0)
                        {
                            resultBuilder.Append(' ');
                        }
                        resultBuilder.Append(result.Text);
                    }
                }
                
                return resultBuilder.ToString();
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
        /// Разбивает данные на указанное количество фрагментов
        /// </summary>
        private List<byte[]> SplitIntoChunks(byte[] data, int numChunks)
        {
            List<byte[]> chunks = new List<byte[]>();
            
            int chunkSize = data.Length / numChunks;
            
            // Делаем размер фрагмента кратным 4 для обеспечения правильной обработки 16-битных стерео данных
            chunkSize = (chunkSize / 4) * 4;
            
            for (int i = 0; i < numChunks; i++)
            {
                int startIndex = i * chunkSize;
                int length = (i < numChunks - 1) ? chunkSize : (data.Length - startIndex);
                
                byte[] chunk = new byte[length];
                Array.Copy(data, startIndex, chunk, 0, length);
                
                chunks.Add(chunk);
            }
            
            return chunks;
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

            // Отписываемся от событий SessionManager
            _sessionManager.RecognitionStarted -= OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted -= OnSessionManagerRecognitionCompleted;

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