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

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Класс для потокового распознавания речи
    /// </summary>
    public class WhisperStreamProcessor : ISpeechRecognitionService, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IAudioProcessor _audioProcessor;
        private readonly ISpeechRecognizer _recognizer;
        private readonly SessionManager _sessionManager;
        private readonly ModelDownloader _modelDownloader;
        private RecognitionSession _currentSession;
        private bool _isDisposed;
        private readonly string _language;
        private int numChunksUsed = 1;

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
            _language = language;

            // Создаем сессию для обработки потока
            _currentSession = _sessionManager.CreateSession();
            
            // Подписываемся на события SessionManager
            _sessionManager.RecognitionStarted += OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted += OnSessionManagerRecognitionCompleted;
            _sessionManager.FragmentStatusChanged += OnSessionManagerFragmentStatusChanged;
            
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
                            var recognizer = new WhisperRecognizer(_audioProcessor, ((WhisperRecognizer)_recognizer).ModelPath, _language);
                            await recognizer.InitializeAsync();
                            
                            // Создаем новую сессию
                            using var session = new RecognitionSession(recognizer);
                            
                            _logger.LogInformation($"Начало распознавания фрагмента #{fragmentIndex + 1} в сессии {session.Id}");
                            
                            // Распознаем речь
                            DateTime startTime = DateTime.Now;
                            var result = await session.ProcessFragmentAsync(wavChunks[fragmentIndex], cancellationToken);
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

        #region Методы управления очередью распознавания

        /// <summary>
        /// Добавляет фрагмент в очередь распознавания
        /// </summary>
        /// <param name="audioChunk">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <returns>Идентификатор элемента очереди</returns>
        public Guid EnqueueRecognitionItem(byte[] audioChunk, int priority = 0, string metadata = "")
        {
            ThrowIfDisposed();
            
            // Проверяем аудиоданные
            if (audioChunk == null || audioChunk.Length == 0)
            {
                throw new ArgumentException("Аудиоданные не могут быть пустыми", nameof(audioChunk));
            }
            
            // Подготавливаем аудиоданные, если нужно
            if (!_audioProcessor.IsWavFile(audioChunk))
            {
                _logger.LogInformation("Добавление WAV-заголовка к данным перед постановкой в очередь");
                audioChunk = _audioProcessor.AddWavHeader(audioChunk);
            }
            
            // Добавляем фрагмент в очередь через SessionManager
            var fragmentIdTask = _sessionManager.EnqueueFragmentAsync(audioChunk, priority, metadata);
            int fragmentId = fragmentIdTask.GetAwaiter().GetResult();
            
            // Формируем уникальный GUID на основе идентификатора сессии и фрагмента
            // для совместимости с интерфейсом
            return GetExternalQueueId(fragmentId);
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
            
            // Преобразуем внешний идентификатор в идентификатор фрагмента
            int fragmentId = GetInternalFragmentId(itemId);
            
            return _sessionManager.ChangeFragmentPriority(fragmentId, newPriority);
        }

        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если обработка успешно отменена</returns>
        public bool CancelQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            // Преобразуем внешний идентификатор в идентификатор фрагмента
            int fragmentId = GetInternalFragmentId(itemId);
            
            return _sessionManager.CancelFragment(fragmentId);
        }

        /// <summary>
        /// Получает информацию о фрагменте в очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>Информация о фрагменте или null, если не найден</returns>
        public object GetQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            // Преобразуем внешний идентификатор в идентификатор фрагмента
            int fragmentId = GetInternalFragmentId(itemId);
            
            // Получаем информацию о фрагменте
            var fragment = _sessionManager.GetFragment(fragmentId);
            
            if (fragment == null)
                return null;
                
            // Преобразуем SessionFragment в RecognitionQueueItem для совместимости с интерфейсом
            return new RecognitionQueueItem(
                fragment.FragmentId, 
                fragment.AudioData, 
                ConvertFragmentStatus(fragment.Status),
                fragment.Priority,
                fragment.Metadata,
                fragment.AddedTime,
                GetExternalQueueId(fragment.FragmentId));
        }

        /// <summary>
        /// Получает список элементов очереди
        /// </summary>
        /// <param name="statusFilter">Фильтр по статусу (null для всех элементов)</param>
        /// <returns>Список элементов очереди</returns>
        public IReadOnlyList<object> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null)
        {
            ThrowIfDisposed();
            
            // Преобразуем внешний статус во внутренний
            FragmentStatus? fragmentStatus = statusFilter.HasValue ? 
                ConvertQueueItemStatus(statusFilter.Value) : null;
                
            // Получаем список фрагментов
            var fragments = _sessionManager.GetFragments(fragmentStatus);
            
            // Преобразуем список фрагментов в список RecognitionQueueItem и приводим к object
            return fragments.Select(f => (object)new RecognitionQueueItem(
                f.FragmentId,
                f.AudioData,
                ConvertFragmentStatus(f.Status),
                f.Priority,
                f.Metadata,
                f.AddedTime,
                GetExternalQueueId(f.FragmentId)
            )).ToList();
        }

        /// <summary>
        /// Приостанавливает обработку очереди
        /// </summary>
        public void PauseQueue()
        {
            ThrowIfDisposed();
            _sessionManager.PauseProcessing();
        }

        /// <summary>
        /// Возобновляет обработку очереди
        /// </summary>
        public void ResumeQueue()
        {
            ThrowIfDisposed();
            _sessionManager.ResumeProcessing();
        }

        /// <summary>
        /// Очищает очередь распознавания
        /// </summary>
        /// <param name="cancelProcessing">Отменять ли текущие операции распознавания</param>
        public void ClearQueue(bool cancelProcessing = false)
        {
            ThrowIfDisposed();
            
            if (cancelProcessing) 
            {
                // Если нужно отменить текущие операции, отменяем все фрагменты со статусом Processing
                var processingFragments = _sessionManager.GetFragments(FragmentStatus.Processing);
                foreach (var fragment in processingFragments)
                {
                    _sessionManager.CancelFragment(fragment.FragmentId);
                }
            }
            
            // Очищаем очередь ожидающих фрагментов
            _sessionManager.ClearPendingFragments();
        }

        /// <summary>
        /// Удаляет элемент из очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если элемент успешно удален</returns>
        public bool RemoveQueueItem(Guid itemId)
        {
            ThrowIfDisposed();
            
            // Преобразуем внешний идентификатор в идентификатор фрагмента
            int fragmentId = GetInternalFragmentId(itemId);
            
            return _sessionManager.CancelFragment(fragmentId);
        }

        /// <summary>
        /// Возвращает количество элементов в очереди с указанным статусом
        /// </summary>
        /// <param name="status">Статус (null для всех элементов)</param>
        /// <returns>Количество элементов</returns>
        public int GetQueueItemCount(RecognitionQueueItemStatus? status = null)
        {
            ThrowIfDisposed();
            
            // Преобразуем внешний статус во внутренний
            FragmentStatus? fragmentStatus = status.HasValue ? 
                ConvertQueueItemStatus(status.Value) : null;
                
            return _sessionManager.GetFragmentsCount(fragmentStatus);
        }
        
        // Вспомогательный класс для совместимости с интерфейсом
        public class RecognitionQueueItem
        {
            public int InternalId { get; }
            public byte[] AudioData { get; }
            public RecognitionQueueItemStatus Status { get; }
            public int Priority { get; }
            public string Metadata { get; }
            public DateTime EnqueueTime { get; }
            public Guid Id { get; }
            
            public RecognitionQueueItem(
                int internalId, 
                byte[] audioData, 
                RecognitionQueueItemStatus status, 
                int priority, 
                string metadata, 
                DateTime enqueueTime,
                Guid id)
            {
                InternalId = internalId;
                AudioData = audioData;
                Status = status;
                Priority = priority;
                Metadata = metadata;
                EnqueueTime = enqueueTime;
                Id = id;
            }
        }
        
        // Вспомогательные методы преобразования
        private Guid GetExternalQueueId(int fragmentId)
        {
            // Упрощенная реализация: используем идентификатор фрагмента как последние 4 байта GUID
            return new Guid(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)fragmentId);
        }
        
        private int GetInternalFragmentId(Guid itemId)
        {
            // Получаем идентификатор фрагмента из последнего байта GUID
            byte[] bytes = itemId.ToByteArray();
            return bytes[15];
        }
        
        private RecognitionQueueItemStatus ConvertFragmentStatus(FragmentStatus status)
        {
            switch (status)
            {
                case FragmentStatus.Pending: return RecognitionQueueItemStatus.Pending;
                case FragmentStatus.Processing: return RecognitionQueueItemStatus.Processing;
                case FragmentStatus.Completed: return RecognitionQueueItemStatus.Completed;
                case FragmentStatus.Failed: return RecognitionQueueItemStatus.Failed;
                case FragmentStatus.Canceled: return RecognitionQueueItemStatus.Canceled;
                default: return RecognitionQueueItemStatus.Pending;
            }
        }
        
        private FragmentStatus ConvertQueueItemStatus(RecognitionQueueItemStatus status)
        {
            switch (status)
            {
                case RecognitionQueueItemStatus.Pending: return FragmentStatus.Pending;
                case RecognitionQueueItemStatus.Processing: return FragmentStatus.Processing;
                case RecognitionQueueItemStatus.Completed: return FragmentStatus.Completed;
                case RecognitionQueueItemStatus.Failed: return FragmentStatus.Failed;
                case RecognitionQueueItemStatus.Canceled: return FragmentStatus.Canceled;
                default: return FragmentStatus.Pending;
            }
        }

        #endregion

        /// <summary>
        /// Освобождает ресурсы
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            // Отписываемся от событий
            _sessionManager.RecognitionStarted -= OnSessionManagerRecognitionStarted;
            _sessionManager.RecognitionCompleted -= OnSessionManagerRecognitionCompleted;
            _sessionManager.FragmentStatusChanged -= OnSessionManagerFragmentStatusChanged;

            // Освобождаем ресурсы
            _sessionManager.Dispose();
            _recognizer.Dispose();

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
    
    /// <summary>
    /// Статус элемента очереди распознавания для внешнего интерфейса
    /// </summary>
    public enum RecognitionQueueItemStatus
    {
        /// <summary>
        /// Ожидает обработки
        /// </summary>
        Pending,

        /// <summary>
        /// Находится в процессе обработки
        /// </summary>
        Processing,

        /// <summary>
        /// Обработка завершена успешно
        /// </summary>
        Completed,

        /// <summary>
        /// Обработка завершена с ошибкой
        /// </summary>
        Failed,

        /// <summary>
        /// Обработка отменена
        /// </summary>
        Canceled
    }
} 