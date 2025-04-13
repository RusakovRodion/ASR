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
using System.Text.RegularExpressions;
using System.Linq;
using NAudio.Wave;
using SpeechRecognition.Core.Recovery;

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Класс для потокового распознавания речи
    /// </summary>
    public class WhisperStreamProcessor : ISpeechRecognitionService, IDisposable, IAsyncDisposable
    {
        private readonly ILogger _logger;
        private readonly IAudioProcessor _audioProcessor;
        private readonly ISpeechRecognizer _recognizer;
        private readonly Sessions.SessionManager _sessionManager;
        private readonly ModelDownloader _modelDownloader;
        private RecognitionSession _currentSession;
        private bool _isDisposed;
        private readonly string _language;
        private int numChunksUsed = 1;
        private readonly CancellationTokenSource _disposalCts = new CancellationTokenSource();

        /// <summary>
        /// Событие, возникающее при получении результата распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionCompleted;

        /// <summary>
        /// Событие, возникающее перед началом распознавания речи
        /// </summary>
        public event EventHandler<RecognitionEventArgs> RecognitionStarted;

        /// <summary>
        /// Событие изменения статуса элемента очереди
        /// </summary>
        public event EventHandler<FragmentStatusChangedEventArgs> QueueItemStatusChanged;
        
        /// <summary>
        /// Событие изменения состояния очереди
        /// </summary>
        public event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        /// <summary>
        /// Событие, возникающее при ошибке распознавания
        /// </summary>
        public event EventHandler<RecognitionErrorEventArgs> RecognitionError;

        /// <summary>
        /// Создает новый экземпляр класса WhisperStreamProcessor с указанным распознавателем
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="recognizer">Распознаватель речи</param>
        public WhisperStreamProcessor(ILogger logger, ISpeechRecognizer recognizer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            
            _audioProcessor = new AudioProcessor();
            _modelDownloader = new ModelDownloader(logger);
            _sessionManager = new Sessions.SessionManager(_recognizer, logger);
            _isDisposed = false;
            _language = recognizer.ModelSettings?.Language ?? "ru";

            // Создаем сессию для обработки потока
            _currentSession = _sessionManager.CreateSession();
            
            // Подписываемся на события SessionManager
            _sessionManager.RecognitionStarted += OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted += OnSessionManagerRecognitionCompleted;
            _sessionManager.FragmentStatusChanged += OnSessionManagerFragmentStatusChanged;
            _sessionManager.QueueStateChanged += OnSessionManagerQueueStateChanged;
            
            _logger.LogInformation($"WhisperStreamProcessor создан с моделью: {recognizer.ModelSettings?.ModelPath ?? "не указана"}, тип: {recognizer.RecognizerType}, язык: {_language}");
        }

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
            var modelSettings = new WhisperModelSettings(modelPath, language, modelType);
            _recognizer = new WhisperRecognizer(_audioProcessor, modelSettings, logger);
            _sessionManager = new Sessions.SessionManager(_recognizer, logger);
            _isDisposed = false;
            _language = language;

            // Создаем сессию для обработки потока
            _currentSession = _sessionManager.CreateSession();
            
            // Подписываемся на события SessionManager
            _sessionManager.RecognitionStarted += OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted += OnSessionManagerRecognitionCompleted;
            _sessionManager.FragmentStatusChanged += OnSessionManagerFragmentStatusChanged;
            _sessionManager.QueueStateChanged += OnSessionManagerQueueStateChanged;
            
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
        
        private void OnSessionManagerFragmentStatusChanged(object sender, FragmentStatusChangedEventArgs e)
        {
            QueueItemStatusChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Обработчик события изменения состояния очереди
        /// </summary>
        private void OnSessionManagerQueueStateChanged(object sender, QueueStateChangedEventArgs e)
        {
            QueueStateChanged?.Invoke(this, e);
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
                // Получаем провайдер моделей и проверяем наличие модели
                var modelProvider = ModelProviderFactory.CreateProvider(_recognizer.ModelSettings, _logger);
                await modelProvider.EnsureModelExistsAsync(_recognizer.ModelSettings);
                
                // Инициализация распознавателя
                await _recognizer.InitializeAsync();
                
                // Создаем новую сессию, если она еще не создана
                if (_currentSession == null)
                {
                    _currentSession = _sessionManager.CreateSession();
                    _logger.LogDebug($"Создана новая сессия с ID: {_currentSession.Id}");
                }
                
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
                throw new ArgumentException("Аудиофрагмент не может быть пустым или null");
            }

            // Проверяем, что сессия инициализирована
            if (_currentSession == null)
            {
                _logger.LogDebug("Текущая сессия не инициализирована, создаем новую");
                _currentSession = _sessionManager.CreateSession();
            }

            try
            {
                // Проверяем, что аудио в WAV формате
                if (!_audioProcessor.IsWavFile(audioChunk))
                {
                    _logger.LogWarning("Аудиоданные не в формате WAV");
                    var ex = new InvalidOperationException("Аудиоданные должны быть в формате WAV");
                    RecognitionError?.Invoke(this, new Events.RecognitionErrorEventArgs(ex, "Неверный формат аудио"));
                    throw ex;
                }

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
                    var ex = new InvalidOperationException("Не удалось получить результат распознавания");
                    RecognitionError?.Invoke(this, new Events.RecognitionErrorEventArgs(ex, "Пустой результат распознавания"));
                    throw ex;
                }
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogInformation("Операция распознавания была отменена");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при распознавании аудиофрагмента");
                RecognitionError?.Invoke(this, new Events.RecognitionErrorEventArgs(ex, "Ошибка при распознавании аудиофрагмента"));
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

            try
            {
                _logger.LogInformation($"Обработка файла {filePath} с разбиением на {numChunks} фрагментов");
                
                // Проверяем, что это WAV файл
                byte[] fileData = await File.ReadAllBytesAsync(filePath, cancellationToken);
                if (!_audioProcessor.IsWavFile(fileData))
                {
                    throw new InvalidOperationException("Файл должен быть в формате WAV");
                }
                
                // Устанавливаем количество используемых чанков
                numChunksUsed = numChunks;
                
                // Используем метод из AudioProcessor для разбиения на фрагменты
                List<byte[]> wavChunks = await _audioProcessor.SplitAudioFileIntoChunksAsync(filePath, numChunks);
                
                _logger.LogInformation($"Файл разбит на {wavChunks.Count} фрагментов");
                
                if (wavChunks.Count <= 1)
                {
                    _logger.LogInformation("Аудиофайл не был разбит на фрагменты (возможно, он слишком короткий), обрабатываем целиком");
                    return await ProcessFileAsync(filePath, cancellationToken);
                }
                
                _logger.LogInformation($"Запуск обработки {wavChunks.Count} фрагментов в отдельных полностью независимых сессиях");
                
                // Создаем реальный вывод для консоли, а не просто логов
                var finalResult = new StringBuilder();
                _logger.LogInformation("Результаты распознавания будут выводиться по мере их получения");
                
                // Запускаем обработку каждого фрагмента в отдельной сессии и не ждем их завершения
                for (int i = 0; i < wavChunks.Count; i++)
                {
                    int fragmentIndex = i; // Копируем индекс для использования в лямбде
                    
                    // Запускаем обработку фрагмента в отдельной задаче
                    _ = Task.Run(async () => 
                    {
                        try
                        {
                            // Создаем новый распознаватель для этой сессии
                            var modelSettings = _recognizer.ModelSettings;
                            var recognizer = SpeechRecognizerFactory.CreateRecognizer(_audioProcessor, modelSettings, _logger);
                            await recognizer.InitializeAsync();
                            
                            // Создаем новую сессию
                            using var session = new RecognitionSession(recognizer, _logger);
                            
                            _logger.LogInformation($"Начало распознавания фрагмента #{fragmentIndex + 1} в сессии {session.Id}");
                            
                            // Распознаем речь
                            DateTime startTime = DateTime.Now;
                            int fragmentId = await session.AddFragmentAsync(wavChunks[fragmentIndex]);
                            var result = await session.ProcessFragmentAsync(fragmentId, cancellationToken);
                            DateTime endTime = DateTime.Now;
                            
                            // Определяем примерную длительность аудиофрагмента в секундах
                            double audioLengthSeconds = 0;
                            using (var stream = new MemoryStream(wavChunks[fragmentIndex]))
                            using (var reader = new WaveFileReader(stream))
                            {
                                audioLengthSeconds = (double)reader.Length / reader.WaveFormat.AverageBytesPerSecond;
                            }
                            
                            // Очищаем текст от шума
                            string cleanedText = CleanupMusic(result.Text);
                            
                            // Форматируем результат с текущим временем
                            string fragmentOutput = $"[{DateTime.Now:HH:mm:ss.fff}] === Фрагмент {fragmentIndex + 1} ===\n{cleanedText}\n";
                            
                            // Выводим результат сразу в консоль
                            _logger.LogInformation($"Завершено распознавание фрагмента #{fragmentIndex + 1} в сессии {session.Id}. Длительность обработки: {(endTime - startTime).TotalSeconds:F2} с, длительность аудио: {audioLengthSeconds:F2} с");
                            _logger.LogInformation(fragmentOutput);
                            
                            // Добавляем результат в общий вывод (хотя в данном случае не важно, т.к. результаты уже выведены)
                            lock (finalResult)
                            {
                                finalResult.AppendLine(fragmentOutput);
                            }
                        }
                        catch (Exception ex)
                        {
                            string errorMessage = $"[{DateTime.Now:HH:mm:ss.fff}] === Фрагмент {fragmentIndex + 1} ===\nОшибка: {ex.Message}\n";
                            _logger.LogError(ex, $"Ошибка при обработке фрагмента #{fragmentIndex + 1}");
                            
                            // Вывод ошибки в консоль
                            _logger.LogError(errorMessage);
                            
                            // Добавляем сообщение об ошибке в общий вывод
                            lock (finalResult)
                            {
                                finalResult.AppendLine(errorMessage);
                            }
                        }
                    }, cancellationToken);
                }
                
                // Возвращаем сообщение о том, что результаты выводятся по мере их получения
                return "Результаты распознавания выводятся по мере их получения.";
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
        /// Очищает повторяющиеся метки [музыка] и другие заполнители в тексте
        /// </summary>
        private string CleanupMusic(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            
            // Создаем список известных меток музыки
            var musicPatterns = new List<string> 
            { 
                @"\[музыка\]", 
                @"\[весёлая музыка\]",
                @"\[фоновая музыка\]",
                @"\[тихая музыка\]",
                @"\[громкая музыка\]",
                @"\[классическая музыка\]",
                @"\[музыкальная заставка\]",
                @"\[музыкальная пауза\]"
            };
            
            string cleaned = text;
            
            // Заменяем последовательности одинаковых меток на одну
            foreach (var musicPattern in musicPatterns)
            {
                string repeatedPattern = $"({musicPattern}\\s*){{2,}}";
                cleaned = Regex.Replace(cleaned, repeatedPattern, m => musicPattern.Replace("\\", "") + " ");
            }
            
            // Общий шаблон для любой музыкальной метки
            string generalMusicPattern = @"(\[(?:\w+\s+)?музыка(?:\s+\w+)?\]\s*){2,}";
            cleaned = Regex.Replace(cleaned, generalMusicPattern, m => m.Groups[1].Value);
            
            // Удаляем технические метки
            cleaned = Regex.Replace(cleaned, @"\[NHSU\]|\bNHSU\b", "");
            cleaned = Regex.Replace(cleaned, @"\[HUD\]|\bHUD\b", "");
            cleaned = Regex.Replace(cleaned, @"\[неразборчиво\]", "");
            cleaned = Regex.Replace(cleaned, @"\[шум\]|\[фоновый шум\]", "");
            
            // Удаляем информацию о редакторе и корректоре, которая часто появляется в конце
            cleaned = Regex.Replace(cleaned, @"редактор субтитров\s*\.[\w\s\.]+", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"корректор\s*\.[\w\s\.]+", "", RegexOptions.IgnoreCase);
            
            // Удаляем короткие непонятные последовательности символов
            cleaned = Regex.Replace(cleaned, @"\b\w{1,2}\b", "");
            
            // Удаляем случайные англоязычные вставки, если язык распознавания - русский
            if (_language == "ru")
            {
                cleaned = Regex.Replace(cleaned, @"\b[a-zA-Z]{1,4}\b", "");
            }
            
            // Очищаем лишние пробелы
            cleaned = Regex.Replace(cleaned, @"\s+", " ");
            
            // Максимум одна метка музыки в начале и в конце
            if (musicPatterns.Any(p => Regex.IsMatch(cleaned, p.Replace("\\", ""))))
            {
                // Удаляем лишние метки музыки в середине текста, оставляя только в начале и конце
                var words = cleaned.Split(' ');
                bool hasStartMusicTag = false;
                bool hasEndMusicTag = false;
                
                // Проверяем, есть ли метка в начале
                foreach (var musicPattern in musicPatterns)
                {
                    string plainPattern = musicPattern.Replace("\\", "");
                    if (words.Length > 0 && words[0] == plainPattern)
                    {
                        hasStartMusicTag = true;
                        break;
                    }
                }
                
                // Проверяем, есть ли метка в конце
                foreach (var musicPattern in musicPatterns)
                {
                    string plainPattern = musicPattern.Replace("\\", "");
                    if (words.Length > 0 && words[words.Length - 1] == plainPattern)
                    {
                        hasEndMusicTag = true;
                        break;
                    }
                }
                
                // Фильтруем промежуточные метки музыки
                List<string> filteredWords = new List<string>();
                if (hasStartMusicTag)
                {
                    filteredWords.Add(words[0]);
                }
                
                for (int i = hasStartMusicTag ? 1 : 0; i < (hasEndMusicTag ? words.Length - 1 : words.Length); i++)
                {
                    bool isMusicTag = false;
                    foreach (var musicPattern in musicPatterns)
                    {
                        string plainPattern = musicPattern.Replace("\\", "");
                        if (words[i] == plainPattern)
                        {
                            isMusicTag = true;
                            break;
                        }
                    }
                    
                    if (!isMusicTag)
                    {
                        filteredWords.Add(words[i]);
                    }
                }
                
                if (hasEndMusicTag)
                {
                    filteredWords.Add(words[words.Length - 1]);
                }
                
                cleaned = string.Join(" ", filteredWords);
            }
            
            return cleaned.Trim();
        }

        /// <summary>
        /// Объединение результатов распознавания
        /// </summary>
        /// <param name="recognizedTexts">Список распознанных текстов по фрагментам</param>
        /// <returns>Объединенный текст</returns>
        private string MergeRecognitionResults(List<string> recognizedTexts)
        {
            if (recognizedTexts == null || recognizedTexts.Count == 0)
            {
                return string.Empty;
            }
            
            if (recognizedTexts.Count == 1)
            {
                return CleanupMusic(recognizedTexts[0]);
            }
            
            _logger.LogDebug($"Объединение {recognizedTexts.Count} фрагментов текста");
            
            // Фильтруем пустые результаты
            recognizedTexts = recognizedTexts.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            
            if (recognizedTexts.Count == 0)
            {
                return string.Empty;
            }
            
            if (recognizedTexts.Count == 1)
            {
                return CleanupMusic(recognizedTexts[0]);
            }
            
            // Очищаем повторяющиеся метки [музыка] в начале и конце фрагментов
            for (int i = 0; i < recognizedTexts.Count; i++)
            {
                recognizedTexts[i] = CleanupMusic(recognizedTexts[i]);
            }
            
            // Проверяем, насколько полезны результаты (если в основном [музыка], то лучше обработать файл целиком)
            bool mostlyMusic = recognizedTexts.Count(t => t.Contains("[музыка]") && t.Replace("[музыка]", "").Trim().Length < 10) > recognizedTexts.Count / 2;
            
            if (mostlyMusic && numChunksUsed > 1)
            {
                _logger.LogWarning("Большинство фрагментов содержат только метки [музыка]. Рекомендуется обработать файл целиком (--num-chunks 1)");
            }
            
            // Просто объединяем тексты с пробелом между ними
            StringBuilder resultBuilder = new StringBuilder();
            
            foreach (var text in recognizedTexts)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    if (resultBuilder.Length > 0)
                    {
                        resultBuilder.Append(" ");
                    }
                    resultBuilder.Append(text);
                }
            }
            
            // Финальная очистка результата
            string result = resultBuilder.ToString().Trim();
            result = CleanupMusic(result);
            
            return result;
        }

        #region Методы управления очередью
        
        /// <summary>
        /// Добавляет фрагмент в очередь распознавания
        /// </summary>
        /// <param name="audioChunk">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <param name="processImmediately">Обработать немедленно, минуя очередь</param>
        /// <returns>Идентификатор элемента очереди и задача для отслеживания результата</returns>
        public async Task<(Guid QueueItemId, Task<string> ResultTask)> EnqueueRecognitionItemAsync(
            byte[] audioChunk, 
            int priority = 0, 
            string metadata = "", 
            bool processImmediately = false)
        {
            ThrowIfDisposed();
            
            if (audioChunk == null || audioChunk.Length == 0)
            {
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioChunk));
            }
            
            try
            {
                // Подготавливаем аудиоданные для распознавания
                byte[] preparedAudio = await _audioProcessor.PrepareAudioDataAsync(audioChunk);
                
                // Добавляем фрагмент в очередь
                var (fragmentId, processingTask) = await _sessionManager.EnqueueFragmentAsync(
                    preparedAudio, priority, metadata, _currentSession.Id, processImmediately);
                
                // Создаем глобальный идентификатор для элемента очереди
                var queueItemId = _sessionManager.GetFragmentGlobalId(_currentSession.Id, fragmentId);
                
                // Преобразуем результат распознавания в текст
                var resultTask = processingTask.ContinueWith(t => 
                {
                    if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "Ошибка при обработке фрагмента {FragmentId}", fragmentId);
                        return string.Empty;
                    }
                    
                    if (t.IsCanceled)
                    {
                        return string.Empty;
                    }
                    
                    var result = t.Result;
                    return result?.Text ?? string.Empty;
                }, TaskContinuationOptions.ExecuteSynchronously);
                
                return (queueItemId, resultTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при добавлении фрагмента в очередь распознавания");
                throw;
            }
        }
        
        /// <summary>
        /// Изменяет приоритет фрагмента в очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <returns>true, если приоритет успешно изменен</returns>
        public bool ChangeItemPriority(Guid itemId, int newPriority)
        {
            ThrowIfDisposed();
            
            try
            {
                return _sessionManager.ChangeFragmentPriority(itemId, newPriority);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при изменении приоритета фрагмента {ItemId}", itemId);
                return false;
            }
        }
        
        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если обработка успешно отменена</returns>
        public bool CancelQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            try
            {
                return _sessionManager.CancelFragment(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене обработки фрагмента {ItemId}", itemId);
                return false;
            }
        }
        
        /// <summary>
        /// Получает информацию о фрагменте в очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>Информация о фрагменте или null, если не найден</returns>
        public QueueItem GetQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            try
            {
                var fragment = _sessionManager.GetFragmentInfo(itemId);
                
                if (fragment == null)
                {
                    return null;
                }
                
                return new QueueItem(
                    itemId,
                    ConvertFragmentStatus(fragment.Status),
                    fragment.Priority,
                    fragment.AddedTime,
                    fragment.Metadata,
                    fragment.AudioData?.Length ?? 0
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении информации о фрагменте {ItemId}", itemId);
                return null;
            }
        }
        
        /// <summary>
        /// Получает список элементов очереди
        /// </summary>
        /// <param name="statusFilter">Фильтр по статусу (null для всех элементов)</param>
        /// <returns>Список элементов очереди</returns>
        public IReadOnlyList<QueueItem> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null)
        {
            ThrowIfDisposed();
            
            try
            {
                // Преобразуем фильтр статуса
                FragmentStatus? fragmentStatus = statusFilter.HasValue ?
                    ConvertQueueItemStatus(statusFilter.Value) : null;
                
                // Получаем фрагменты из менеджера сессий, указывая ID текущей сессии
                var fragments = _sessionManager.GetFragments(fragmentStatus, _currentSession.Id);
                
                // Преобразуем фрагменты в элементы очереди
                return fragments.Select(fragment => 
                {
                    var queueItemId = _sessionManager.GetFragmentGlobalId(_currentSession.Id, fragment.FragmentId);
                    
                    return new QueueItem(
                        queueItemId,
                        ConvertFragmentStatus(fragment.Status),
                        fragment.Priority,
                        fragment.AddedTime,
                        fragment.Metadata,
                        fragment.AudioData?.Length ?? 0
                    );
                }).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении списка элементов очереди");
                return Array.Empty<QueueItem>();
            }
        }
        
        /// <summary>
        /// Приостанавливает обработку очереди
        /// </summary>
        public void PauseQueue()
        {
            ThrowIfDisposed();
            
            try
            {
                _sessionManager.PauseProcessing();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при приостановке обработки очереди");
            }
        }
        
        /// <summary>
        /// Возобновляет обработку очереди
        /// </summary>
        public void ResumeQueue()
        {
            ThrowIfDisposed();
            
            try
            {
                _sessionManager.ResumeProcessing();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при возобновлении обработки очереди");
            }
        }
        
        /// <summary>
        /// Очищает очередь распознавания
        /// </summary>
        /// <param name="cancelProcessing">Отменять ли текущие операции распознавания</param>
        public void ClearQueue(bool cancelProcessing = false)
        {
            ThrowIfDisposed();
            
            try
            {
                _sessionManager.ClearQueue(null, cancelProcessing);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке очереди распознавания");
            }
        }
        
        /// <summary>
        /// Отменяет все операции распознавания
        /// </summary>
        public void CancelAllOperations()
        {
            ThrowIfDisposed();
            
            try
            {
                _sessionManager.CancelAllOperations();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отмене всех операций распознавания");
            }
        }
        
        /// <summary>
        /// Удаляет элемент из очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если элемент успешно удален</returns>
        public bool RemoveQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            try
            {
                return _sessionManager.CancelFragment(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении элемента из очереди {ItemId}", itemId);
                return false;
            }
        }
        
        /// <summary>
        /// Возвращает количество элементов в очереди с указанным статусом
        /// </summary>
        /// <param name="status">Статус (null для всех элементов)</param>
        /// <returns>Количество элементов</returns>
        public int GetQueueItemCount(RecognitionQueueItemStatus? status = null)
        {
            ThrowIfDisposed();
            
            try
            {
                // Преобразуем фильтр статуса
                FragmentStatus? fragmentStatus = status.HasValue ?
                    ConvertQueueItemStatus(status.Value) : null;
                
                return _sessionManager.GetFragmentsCount(fragmentStatus, _currentSession.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении количества элементов в очереди");
                return 0;
            }
        }
        
        /// <summary>
        /// Запускает обработку очереди с указанным уровнем параллелизма
        /// </summary>
        /// <param name="maxParallelProcessing">Максимальное количество одновременно обрабатываемых фрагментов</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        public async Task ProcessQueueAsync(int maxParallelProcessing = 1, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            // Комбинируем токены отмены
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                _disposalCts.Token);
            
            try
            {
                await _currentSession.ProcessQueueAsync(maxParallelProcessing, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Нормальная отмена, просто возвращаемся
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обработке очереди распознавания");
                throw;
            }
        }
        
        // Вспомогательные методы для преобразования статусов
        private RecognitionQueueItemStatus ConvertFragmentStatus(FragmentStatus status)
        {
            switch (status)
            {
                case FragmentStatus.Pending:
                    return RecognitionQueueItemStatus.Pending;
                case FragmentStatus.Processing:
                    return RecognitionQueueItemStatus.Processing;
                case FragmentStatus.Completed:
                    return RecognitionQueueItemStatus.Completed;
                case FragmentStatus.Failed:
                    return RecognitionQueueItemStatus.Failed;
                case FragmentStatus.Canceled:
                    return RecognitionQueueItemStatus.Canceled;
                default:
                    return RecognitionQueueItemStatus.Pending;
            }
        }
        
        private FragmentStatus ConvertQueueItemStatus(RecognitionQueueItemStatus status)
        {
            switch (status)
            {
                case RecognitionQueueItemStatus.Pending:
                    return FragmentStatus.Pending;
                case RecognitionQueueItemStatus.Processing:
                    return FragmentStatus.Processing;
                case RecognitionQueueItemStatus.Completed:
                    return FragmentStatus.Completed;
                case RecognitionQueueItemStatus.Failed:
                    return FragmentStatus.Failed;
                case RecognitionQueueItemStatus.Canceled:
                    return FragmentStatus.Canceled;
                default:
                    return FragmentStatus.Pending;
            }
        }
        
        #endregion
        
        /// <summary>
        /// Повторно обрабатывает фрагмент, завершившийся с ошибкой
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если фрагмент поставлен на повторную обработку, иначе false</returns>
        public bool RetryFailedQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            try
            {
                return _sessionManager.RetryFailedFragment(itemId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при повторной обработке фрагмента {ItemId}", itemId);
                return false;
            }
        }
        
        /// <summary>
        /// Повторно обрабатывает все фрагменты, завершившиеся с ошибкой
        /// </summary>
        /// <returns>Количество фрагментов, поставленных на повторную обработку</returns>
        public int RetryAllFailedQueueItems()
        {
            ThrowIfDisposed();
            
            try
            {
                return _sessionManager.RetryAllFailedFragments(_currentSession.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при повторной обработке всех фрагментов с ошибками");
                return 0;
            }
        }
        
        /// <summary>
        /// Получает статистику по обработке фрагментов
        /// </summary>
        /// <returns>Словарь со статистикой по каждому статусу</returns>
        public Dictionary<RecognitionQueueItemStatus, int> GetQueueStatistics()
        {
            ThrowIfDisposed();
            
            try
            {
                var stats = _sessionManager.GetFragmentsStatistics(_currentSession.Id);
                
                // Преобразуем статусы из FragmentStatus в RecognitionQueueItemStatus
                var result = new Dictionary<RecognitionQueueItemStatus, int>();
                foreach (var pair in stats)
                {
                    result[ConvertFragmentStatus(pair.Key)] = pair.Value;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при получении статистики очереди");
                return new Dictionary<RecognitionQueueItemStatus, int>();
            }
        }
        
        /// <summary>
        /// Настраивает политику восстановления после сбоев
        /// </summary>
        /// <param name="maxRetryCount">Максимальное количество повторных попыток</param>
        /// <param name="retryDelayMs">Базовая задержка перед повтором (в миллисекундах)</param>
        /// <param name="backoffMultiplier">Множитель для экспоненциального увеличения задержки</param>
        public void ConfigureRetryPolicy(int maxRetryCount = 3, int retryDelayMs = 1000, double backoffMultiplier = 1.5)
        {
            ThrowIfDisposed();
            
            try
            {
                var options = new RetryPolicyOptions
                {
                    MaxRetryCount = maxRetryCount,
                    RetryDelayMs = retryDelayMs,
                    BackoffMultiplier = backoffMultiplier
                };
                
                // Создаем и применяем новую политику восстановления
                RetryPolicy retryPolicy = RetryPolicyFactory.Create(_logger, options);
                
                _logger.LogInformation($"Настроена политика восстановления: максимум попыток = {maxRetryCount}, " +
                                      $"задержка = {retryDelayMs}мс, множитель = {backoffMultiplier}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при настройке политики восстановления");
                throw;
            }
        }

        /// <summary>
        /// Асинхронно освобождает ресурсы
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                // Отменяем все текущие операции
                _disposalCts.Cancel();
                
                // Даем небольшую задержку для завершения операций
                await Task.Delay(500);
                
                // Освобождаем ресурсы асинхронно
                if (_sessionManager is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else
                {
                    _sessionManager.Dispose();
                }
                
                _recognizer.Dispose();
                _disposalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при асинхронном освобождении ресурсов");
            }

            _isDisposed = true;
            GC.SuppressFinalize(this);
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

            try
            {
                // Отменяем все текущие операции
                _disposalCts.Cancel();
                
                // Отписываемся от событий
                _sessionManager.RecognitionStarted -= OnSessionManagerRecognitionStarted;
                _sessionManager.RecognitionCompleted -= OnSessionManagerRecognitionCompleted;
                _sessionManager.FragmentStatusChanged -= OnSessionManagerFragmentStatusChanged;
                _sessionManager.QueueStateChanged -= OnSessionManagerQueueStateChanged;

                // Освобождаем ресурсы
                _sessionManager.Dispose();
                _recognizer.Dispose();
                _disposalCts.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при освобождении ресурсов");
            }

            _isDisposed = true;
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