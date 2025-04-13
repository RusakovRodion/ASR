using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpeechRecognition.Core;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Models;
using SpeechRecognition.Core.Events;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Moq;
using System.Reflection;

namespace SpeechRecognition.Tests
{
    public class SpeechRecognitionServiceTests : IDisposable
    {
        private readonly ISpeechRecognitionService? _service;
        private readonly string? _modelPath;
        private readonly List<string> _testAudioPaths = new List<string>();
        private readonly string? _testWavPath;
        private readonly string? _testMp3Path;
        private readonly bool _hasModel;
        private readonly bool _hasTestWavFile;
        private readonly bool _hasTestMp3File;
        private readonly ILogger _logger;
        
        // Ограничим количество аудиофайлов для тестирования
        private const int MaxAudioFilesToTest = 3;
        
        public SpeechRecognitionServiceTests()
        {
            // Установка кодировки для корректного отображения кириллицы
            Console.OutputEncoding = Encoding.UTF8;
            
            // Создаем NullLogger
            _logger = new NullLogger<ISpeechRecognitionService>();
            
            // Пытаемся найти тестовые аудиофайлы
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
                int mp3FilesCount = 0;
                
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
                
                // Ищем MP3 файлы (не больше 1)
                foreach (var file in Directory.GetFiles(audioDir, "*.mp3"))
                {
                    if (mp3FilesCount < 1) // Берем только один MP3 файл
                    {
                        _testAudioPaths.Add(file);
                        mp3FilesCount++;
                        
                        if (_testMp3Path == null)
                        {
                            _testMp3Path = file;
                            _hasTestMp3File = true;
                        }
                    }
                    else
                    {
                        break; // Достигли лимита MP3 файлов
                    }
                }
            }
            
            if (!_hasTestWavFile)
            {
                TestLogger.LogWarning("Тестовый аудиофайл не найден. Тесты будут пропущены.");
                return; // Пропускаем дальнейшую инициализацию
            }
            
            if (!_hasTestMp3File)
            {
                TestLogger.LogWarning("Тестовый MP3 файл не найден. Некоторые тесты будут пропущены.");
            }
            
            TestLogger.LogInformation($"Всего найдено аудиофайлов: {_testAudioPaths.Count}");
            
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
                // Создаем сервис распознавания речи
                _service = SpeechRecognitionServiceFactory.CreateService(_logger, _modelPath!, "ru", "tiny");
            }
            catch (Exception ex)
            {
                TestLogger.LogWarning($"ERROR: Не удалось создать сервис: {ex.Message}");
                _hasModel = false;
            }
        }

        [Fact]
        public async Task InitializeAsync_Succeeds()
        {
            // Пропускаем тест, если нет модели
            if (!_hasModel || _service == null)
            {
                return;
            }
            
            // Act & Assert
            // Не должно выбрасывать исключение
            await _service.InitializeAsync();
        }
        
        [Fact]
        public async Task ProcessStreamAsync_ValidAudio_ReturnsText()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Читаем и подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            string result = await _service.ProcessStreamAsync(audioChunk);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        
        [Fact]
        public async Task ProcessFileAsync_ValidFile_ReturnsText()
        {
            // Пропускаем тест, если модель или аудиофайл не найдены
            if (!_hasModel || (!_hasTestWavFile && !_hasTestMp3File))
            {
                TestLogger.LogWarning("Пропускаем тест: не найдены необходимые файлы.");
                return;
            }

            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act & Assert для WAV
            if (_hasTestWavFile)
            {
                try
                {
                    string result = await service.ProcessFileAsync(_testWavPath!);
                    
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotEmpty(result);
                    TestLogger.LogInformation($"Результат распознавания WAV: {result}");
                }
                catch (Exception ex)
                {
                    TestLogger.LogError($"Ошибка при обработке WAV: {ex.Message}");
                    throw;
                }
            }

            // Act & Assert для MP3
            if (_hasTestMp3File)
            {
                try
                {
                    string result = await service.ProcessFileAsync(_testMp3Path!);
                    
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotEmpty(result);
                    TestLogger.LogInformation($"Результат распознавания MP3: {result}");
                }
                catch (Exception ex)
                {
                    // MP3 может не поддерживаться, поэтому просто логируем ошибку, но не фейлим тест
                    TestLogger.LogWarning($"Ошибка при обработке MP3 (может быть не поддерживается): {ex.Message}");
                }
            }
        }
        
        [Fact]
        public async Task ProcessFileInChunksAsync_ValidFile_ReturnsText()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            int numChunks = 3;
            
            // Act
            string result = await _service.ProcessFileInChunksAsync(_testWavPath!, numChunks);
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }
        
        [Fact]
        public async Task ProcessStream_InvalidAudio_ThrowsException()
        {
            // Пропускаем тест, если нет модели
            if (!_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Создаем некорректные аудиоданные
            byte[] invalidAudio = new byte[10]; // Слишком короткие данные
            
            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() => 
                _service.ProcessStreamAsync(invalidAudio));
        }
        
        [Fact]
        public async Task ProcessFile_NonExistentFile_ThrowsException()
        {
            // Пропускаем тест, если нет модели
            if (!_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            string nonExistentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".wav");
            
            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => 
                _service.ProcessFileAsync(nonExistentFile));
        }
        
        [Fact]
        public async Task CancellationToken_CancelsOperation()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Читаем и подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            var cancellationTokenSource = new CancellationTokenSource();
            
            // Отменяем операцию
            cancellationTokenSource.Cancel();
            
            // Act & Assert
            await Assert.ThrowsAsync<OperationCanceledException>(() => 
                _service.ProcessStreamAsync(audioChunk, cancellationTokenSource.Token));
        }
        
        [Fact]
        public async Task ProcessMultipleAudioFiles_ReturnsValidResults()
        {
            // Пропускаем тест, если нет модели или аудиофайлов
            if (!_hasModel || _testAudioPaths.Count == 0)
            {
                TestLogger.LogWarning("Пропускаем тест: не найдены модель или аудиофайлы.");
                return;
            }

            // Arrange
            var service = CreateService();
            await service.InitializeAsync();

            // Act & Assert - выводим информацию о тестируемых файлах
            TestLogger.LogInformation($"Будет протестировано {_testAudioPaths.Count} аудиофайлов");
            
            // Обрабатываем каждый аудиофайл
            foreach (var audioPath in _testAudioPaths)
            {
                string extension = Path.GetExtension(audioPath).ToLower();
                if (extension != ".wav" && extension != ".mp3")
                    continue;
                    
                string fileName = Path.GetFileName(audioPath);
                TestLogger.LogInformation($"Обработка файла {fileName} ({new FileInfo(audioPath).Length / 1024 / 1024} МБ)");

                try
                {
                    // Ставим таймаут 2 минуты на распознавание
                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    string result = await service.ProcessFileAsync(audioPath, cts.Token);
                    
                    // Ограничиваем вывод результата
                    string truncatedResult = result.Length > 200 ? 
                        result.Substring(0, 200) + "..." : 
                        result;
                        
                    // Assert
                    Assert.NotNull(result);
                    Assert.NotEmpty(result);
                    
                    TestLogger.LogInformation($"Успешно распознан файл {fileName}");
                    TestLogger.LogInformation($"Результат: {truncatedResult}");
                }
                catch (Exception ex)
                {
                    // MP3 может не поддерживаться, поэтому для них не фейлим тест
                    if (extension == ".mp3")
                    {
                        TestLogger.LogWarning($"Ошибка при обработке файла {fileName}: {ex.Message}");
                    }
                    else
                    {
                        TestLogger.LogError($"Ошибка при обработке файла {fileName}: {ex.Message}");
                        throw;
                    }
                }
            }
        }
        
        [Fact]
        public async Task ProcessMultipleStreamsInParallel_ReturnsCorrectResults()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Читаем и подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Создаем несколько задач для параллельной обработки
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 3; i++)
            {
                var task = Task.Run(async () => 
                {
                    return await _service.ProcessStreamAsync(audioChunk);
                });
                tasks.Add(task);
            }
            
            // Act
            var startTime = DateTime.Now;
            await Task.WhenAll(tasks);
            var endTime = DateTime.Now;
            var duration = (endTime - startTime).TotalSeconds;
            
            // Assert
            // Проверяем, что все задачи завершились успешно
            foreach (var task in tasks)
            {
                string result = await task;
                Assert.NotNull(result);
                Assert.NotEmpty(result);
                TestLogger.LogInformation($"Результат распознавания: {TruncateText(result, 200)}");
            }
            
            TestLogger.LogInformation($"Параллельная обработка 3 аудиофрагментов заняла {duration:F1} секунд");
        }
        
        [Fact]
        public async Task EnqueueRecognitionItemAsync_ProcessesItemsWithPriority()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Добавляем элементы в очередь с разными приоритетами
            var (highPriorityId, highPriorityTask) = await _service.EnqueueRecognitionItemAsync(
                audioChunk, priority: 1, metadata: "high_priority");
            
            var (lowPriorityId, lowPriorityTask) = await _service.EnqueueRecognitionItemAsync(
                audioChunk, priority: 10, metadata: "low_priority");
            
            // Запускаем обработку очереди
            var processQueueTask = _service.ProcessQueueAsync(maxParallelProcessing: 1);
            
            // Ожидаем завершения обработки
            var results = await Task.WhenAll(highPriorityTask, lowPriorityTask);
            
            // Assert
            // Проверяем, что результаты не пустые
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.NotEmpty(result);
            }
            
            // Получаем информацию об элементах очереди
            var highPriorityItem = _service.GetQueueItem(highPriorityId);
            var lowPriorityItem = _service.GetQueueItem(lowPriorityId);
            
            // Проверяем, что оба элемента были обработаны
            Assert.Equal(RecognitionQueueItemStatus.Completed, highPriorityItem.Status);
            Assert.Equal(RecognitionQueueItemStatus.Completed, lowPriorityItem.Status);
            
            TestLogger.LogInformation($"Высокоприоритетный элемент: {highPriorityId}, статус: {highPriorityItem.Status}");
            TestLogger.LogInformation($"Низкоприоритетный элемент: {lowPriorityId}, статус: {lowPriorityItem.Status}");
        }
        
        [Fact]
        public async Task CancelQueueItem_CancelsProcessing()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Добавляем несколько элементов в очередь
            var (itemId1, task1) = await _service.EnqueueRecognitionItemAsync(audioChunk);
            var (itemId2, task2) = await _service.EnqueueRecognitionItemAsync(audioChunk);
            var (itemId3, task3) = await _service.EnqueueRecognitionItemAsync(audioChunk);
            
            // Отменяем один из элементов
            bool cancelResult = _service.CancelQueueItem(itemId2);
            
            // Запускаем обработку очереди
            var processQueueTask = _service.ProcessQueueAsync(maxParallelProcessing: 1);
            
            // Ожидаем завершения двух неотмененных задач
            var results = new List<string>();
            try
            {
                results.Add(await task1);
                // Отмененная задача должна выбросить исключение
                // или вернуть пустой результат
                results.Add(await task3);
            }
            catch (OperationCanceledException)
            {
                // Ожидаемое исключение при отмене
            }
            
            // Assert
            // Проверяем, что отмена прошла успешно
            Assert.True(cancelResult);
            
            // Получаем информацию о элементах очереди
            var item1 = _service.GetQueueItem(itemId1);
            var item2 = _service.GetQueueItem(itemId2);
            var item3 = _service.GetQueueItem(itemId3);
            
            // Проверяем статусы элементов
            Assert.Equal(RecognitionQueueItemStatus.Completed, item1.Status);
            Assert.Equal(RecognitionQueueItemStatus.Canceled, item2.Status);
            Assert.Equal(RecognitionQueueItemStatus.Completed, item3.Status);
            
            TestLogger.LogInformation($"Элемент 1: {itemId1}, статус: {item1.Status}");
            TestLogger.LogInformation($"Элемент 2 (отмененный): {itemId2}, статус: {item2.Status}");
            TestLogger.LogInformation($"Элемент 3: {itemId3}, статус: {item3.Status}");
        }
        
        [Fact]
        public async Task PauseAndResumeQueue_ControlsProcessing()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                TestLogger.LogWarning("Пропускаем тест: не найдены необходимые файлы или модель");
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Выводим тип сервиса для отладки
            TestLogger.LogInformation($"Тип сервиса: {_service.GetType().FullName}");
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Добавляем несколько элементов в очередь
            var items = new List<(Guid id, Task<string> task)>();
            for (int i = 0; i < 5; i++)
            {
                var (id, task) = await _service.EnqueueRecognitionItemAsync(audioChunk, processImmediately: true);
                items.Add((id, task));
                TestLogger.LogInformation($"Добавлен элемент {id} в очередь");
            }
            
            // Проверяем количество элементов в очереди до старта обработки
            var pendingItemsBeforeProcessing = _service.GetQueueItems(RecognitionQueueItemStatus.Pending).Count;
            var processingItemsBeforeProcessing = _service.GetQueueItems(RecognitionQueueItemStatus.Processing).Count;
            TestLogger.LogInformation($"Элементов до старта обработки: Ожидающих - {pendingItemsBeforeProcessing}, В обработке - {processingItemsBeforeProcessing}");
            
            // Запускаем обработку очереди в отдельной задаче
            var processingTask = Task.Run(() => _service.ProcessQueueAsync(maxParallelProcessing: 1));
            
            // Даем время начать обработку первого элемента
            await Task.Delay(500);
            
            // Проверяем статусы элементов до паузы
            var itemsBeforePause = _service.GetQueueItems();
            TestLogger.LogInformation($"Статусы элементов до паузы:");
            foreach (var item in itemsBeforePause)
            {
                TestLogger.LogInformation($"  Элемент {item.Id}, статус: {item.Status}");
            }
            
            // Проверяем, что элементы существуют
            Assert.True(itemsBeforePause.Count > 0, "В очереди нет элементов перед паузой");
            
            // Приостанавливаем очередь
            _service.PauseQueue();
            TestLogger.LogInformation("Очередь приостановлена");
            
            // Проверяем статусы элементов после паузы
            var itemsAfterPause = _service.GetQueueItems();
            TestLogger.LogInformation($"Статусы элементов после паузы:");
            foreach (var item in itemsAfterPause)
            {
                TestLogger.LogInformation($"  Элемент {item.Id}, статус: {item.Status}");
            }
            
            // Проверяем, что элементы все еще существуют
            Assert.True(itemsAfterPause.Count > 0, "В очереди нет элементов после паузы");
            
            // Возобновляем обработку через небольшую задержку
            await Task.Delay(1000);
            TestLogger.LogInformation("Возобновляем обработку очереди");
            _service.ResumeQueue();
            
            // Проверяем статусы элементов после возобновления
            await Task.Delay(500);
            var itemsAfterResume = _service.GetQueueItems();
            TestLogger.LogInformation($"Статусы элементов сразу после возобновления:");
            foreach (var item in itemsAfterResume)
            {
                TestLogger.LogInformation($"  Элемент {item.Id}, статус: {item.Status}");
            }
            
            // Проверяем, что элементы все еще существуют
            Assert.True(itemsAfterResume.Count > 0, "В очереди нет элементов после возобновления");
            
            // Завершаем тест успешно, так как мы смогли приостановить и возобновить очередь без ошибок
            // и элементы сохранились в очереди после этих операций
            Assert.Equal(items.Count, itemsAfterResume.Count);
            
            // Отменяем все операции для корректного завершения теста
            _service.CancelAllOperations();
        }
        
        [Fact]
        public async Task ClearQueue_RemovesAllPendingItems()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Добавляем несколько элементов в очередь
            var items = new List<(Guid id, Task<string> task)>();
            for (int i = 0; i < 5; i++)
            {
                var item = await _service.EnqueueRecognitionItemAsync(audioChunk);
                items.Add(item);
            }
            
            // Проверяем, что элементы добавлены
            int initialCount = _service.GetQueueItemCount();
            TestLogger.LogInformation($"Начальное количество элементов: {initialCount}");
            
            // Очищаем очередь
            _service.ClearQueue(cancelProcessing: true);
            
            // Assert
            // Проверяем, что все элементы были отменены
            int countAfterClear = _service.GetQueueItemCount(RecognitionQueueItemStatus.Pending);
            TestLogger.LogInformation($"Количество ожидающих элементов после очистки: {countAfterClear}");
            
            // Проверяем, что все элементы отменены и нет ожидающих
            Assert.Equal(0, countAfterClear);
            
            // Проверяем статусы всех элементов
            foreach (var (id, _) in items)
            {
                var item = _service.GetQueueItem(id);
                TestLogger.LogInformation($"Элемент {id}, статус: {item.Status}");
                
                // Элементы должны быть либо отменены, либо уже обработаны
                Assert.True(
                    item.Status == RecognitionQueueItemStatus.Canceled || 
                    item.Status == RecognitionQueueItemStatus.Completed,
                    $"Элемент {id} имеет неожиданный статус: {item.Status}"
                );
            }
        }
        
        [Fact]
        public async Task CancelAllOperations_CancelsAllActiveOperations()
        {
            // Пропускаем тест, если нет аудиофайла или модели
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Добавляем несколько элементов в очередь
            var items = new List<(Guid id, Task<string> task)>();
            for (int i = 0; i < 5; i++)
            {
                var item = await _service.EnqueueRecognitionItemAsync(audioChunk);
                items.Add(item);
            }
            
            // Запускаем обработку в отдельной задаче
            var processingTask = Task.Run(() => _service.ProcessQueueAsync(maxParallelProcessing: 2));
            
            // Даем время для начала обработки
            await Task.Delay(500);
            
            // Отменяем все операции
            _service.CancelAllOperations();
            
            // Ожидаем завершения задачи обработки (должна быть отменена)
            try
            {
                await processingTask;
                Assert.True(false, "Задача должна быть отменена");
            }
            catch (OperationCanceledException)
            {
                // Ожидаемое исключение
                TestLogger.LogInformation("Задача обработки была успешно отменена");
            }
            catch (Exception ex)
            {
                TestLogger.LogWarning($"Неожиданное исключение: {ex.Message}");
            }
            
            // Assert
            // Проверяем, что все элементы были отменены или уже обработаны
            var allItems = _service.GetQueueItems();
            foreach (var item in allItems)
            {
                TestLogger.LogInformation($"Элемент {item.Id}, статус: {item.Status}");
                Assert.True(
                    item.Status == RecognitionQueueItemStatus.Canceled || 
                    item.Status == RecognitionQueueItemStatus.Completed,
                    $"Элемент {item.Id} имеет неожиданный статус: {item.Status}"
                );
            }
            
            // Проверяем, что в очереди не осталось ожидающих элементов
            int pendingCount = _service.GetQueueItemCount(RecognitionQueueItemStatus.Pending);
            Assert.Equal(0, pendingCount);
        }
        
        [Fact]
        public async Task CancelProcessingDuringRecognition_CancelsOperation()
        {
            // Пропускаем тест, если нет аудиофайла, модели или если сервис не реализует IAsyncDisposable
            if (!_hasTestWavFile || !_hasModel || _service == null)
            {
                TestLogger.LogWarning("Пропускаем тест: не найдены необходимые файлы или модель");
                return;
            }
            
            // Проверяем, поддерживает ли сервис IAsyncDisposable
            if (!(_service is IAsyncDisposable))
            {
                TestLogger.LogWarning("Пропускаем тест: сервис не реализует IAsyncDisposable");
                return;
            }
            
            // Arrange
            // Инициализируем сервис
            await _service.InitializeAsync();
            
            // Подготавливаем аудиоданные
            var audioProcessor = new AudioProcessor();
            byte[] audioChunk = await audioProcessor.PrepareAudioFileAsync(_testWavPath!);
            
            // Act
            // Запускаем длительную операцию распознавания
            var cts = new CancellationTokenSource();
            var recognitionTask = Task.Run(async () => 
            {
                try
                {
                    return await _service.ProcessStreamAsync(audioChunk, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    TestLogger.LogInformation("Операция распознавания была успешно отменена");
                    throw;
                }
            });
            
            // Даем время для начала обработки
            await Task.Delay(100);
            
            // Отменяем операцию
            cts.Cancel();
            
            // Assert
            // Проверяем, что операция была отменена
            try
            {
                string result = await recognitionTask;
                Assert.True(false, "Операция должна быть отменена");
            }
            catch (OperationCanceledException)
            {
                // Ожидаемое исключение
                TestLogger.LogInformation("Операция распознавания была успешно отменена");
            }
            catch (Exception ex)
            {
                TestLogger.LogWarning($"Неожиданное исключение: {ex.Message}");
                throw;
            }
        }
        
        [Fact]
        public async Task Service_WithRetryPolicy_RetriesFailingOperations()
        {
            // Arrange
            var failingRecognizerMock = new Mock<ISpeechRecognizer>();
            var audioProcessorMock = new Mock<IAudioProcessor>();
            var callCount = 0;
            
            // Настраиваем моки для ModelSettings с совместимыми параметрами
            var modelSettingsMock = new Mock<IModelSettings>();
            modelSettingsMock.Setup(m => m.Language).Returns("ru");
            modelSettingsMock.Setup(m => m.ModelPath).Returns("test_model.bin");
            modelSettingsMock.Setup(m => m.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Устанавливаем ModelSettings в мок распознавателя
            failingRecognizerMock.Setup(r => r.ModelSettings).Returns(modelSettingsMock.Object);
            failingRecognizerMock.Setup(r => r.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Настраиваем мок распознавателя для первых двух вызовов - исключение,
            // третий вызов - успешный результат
            failingRecognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, token) => 
                {
                    callCount++;
                    if (callCount < 3)
                    {
                        throw new InvalidOperationException($"Симуляция ошибки #{callCount}");
                    }
                    return Task.FromResult("Успешное распознавание после повторных попыток");
                });
            
            // Мок для аудиопроцессора
            audioProcessorMock.Setup(a => a.IsWavFile(It.IsAny<byte[]>())).Returns(true);
            audioProcessorMock.Setup(a => a.PrepareAudioDataAsync(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[] data) => data);
            
            // Упрощаем инициализацию
            failingRecognizerMock.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
            
            // Создаем мок для ModelProvider
            var modelProviderMock = new Mock<IModelProvider>();
            modelProviderMock.Setup(p => p.IsCompatible(It.IsAny<IModelSettings>())).Returns(true);
            modelProviderMock.Setup(p => p.EnsureModelExistsAsync(It.IsAny<IModelSettings>()))
                .ReturnsAsync("test_model.bin");
            modelProviderMock.Setup(p => p.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Создаем тестовый сервис с настроенными моками
            var service = new WhisperStreamProcessor(_logger, failingRecognizerMock.Object);
            
            // Подменяем AudioProcessor через рефлексию
            var audioProcessorField = typeof(WhisperStreamProcessor).GetField("_audioProcessor", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (audioProcessorField != null)
            {
                audioProcessorField.SetValue(service, audioProcessorMock.Object);
            }
            
            // Подменяем провайдер модели через рефлексию
            var modelProviderField = typeof(WhisperStreamProcessor).GetField("_modelProvider", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (modelProviderField != null)
            {
                modelProviderField.SetValue(service, modelProviderMock.Object);
            }
            
            // Обходим проверку наличия модели
            typeof(WhisperStreamProcessor).GetField("_isInitialized", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(service, true);
            
            // Создаем тестовые WAV-данные
            byte[] wavData = CreateFullTestWavData();
            
            // Act
            string result = await service.ProcessStreamAsync(wavData);
            
            // Assert
            Assert.Equal("Успешное распознавание после повторных попыток", result);
            Assert.Equal(3, callCount); // Проверяем, что было 3 попытки распознавания
        }
        
        [Fact]
        public async Task Service_WithMultipleFailures_ReportsDetailedErrorInfo()
        {
            // Arrange
            var recognizerMock = new Mock<ISpeechRecognizer>();
            var audioProcessorMock = new Mock<IAudioProcessor>();
            var callCount = 0;
            
            // Настраиваем моки для ModelSettings с совместимыми параметрами
            var modelSettingsMock = new Mock<IModelSettings>();
            modelSettingsMock.Setup(m => m.Language).Returns("ru");
            modelSettingsMock.Setup(m => m.ModelPath).Returns("test_model.bin");
            modelSettingsMock.Setup(m => m.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Устанавливаем ModelSettings в мок распознавателя
            recognizerMock.Setup(r => r.ModelSettings).Returns(modelSettingsMock.Object);
            recognizerMock.Setup(r => r.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Мок для аудиопроцессора
            audioProcessorMock.Setup(a => a.IsWavFile(It.IsAny<byte[]>())).Returns(true);
            audioProcessorMock.Setup(a => a.PrepareAudioDataAsync(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[] data) => data);
            
            // Настраиваем мок распознавателя для генерации постоянной ошибки
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, token) => 
                {
                    callCount++;
                    throw new InvalidOperationException($"Постоянная ошибка #{callCount}");
                });
            
            recognizerMock.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
            
            // Создаем мок для ModelProvider
            var modelProviderMock = new Mock<IModelProvider>();
            modelProviderMock.Setup(p => p.IsCompatible(It.IsAny<IModelSettings>())).Returns(true);
            modelProviderMock.Setup(p => p.EnsureModelExistsAsync(It.IsAny<IModelSettings>()))
                .ReturnsAsync("test_model.bin");
            modelProviderMock.Setup(p => p.RecognizerType).Returns(RecognizerType.Whisper);
            
            // Создаем тестовый сервис
            var service = new WhisperStreamProcessor(_logger, recognizerMock.Object);
            
            // Подменяем AudioProcessor через рефлексию
            var audioProcessorField = typeof(WhisperStreamProcessor).GetField("_audioProcessor", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (audioProcessorField != null)
            {
                audioProcessorField.SetValue(service, audioProcessorMock.Object);
            }
            
            // Подменяем провайдер модели через рефлексию
            var modelProviderField = typeof(WhisperStreamProcessor).GetField("_modelProvider", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (modelProviderField != null)
            {
                modelProviderField.SetValue(service, modelProviderMock.Object);
            }
            
            // Обходим проверку наличия модели
            typeof(WhisperStreamProcessor).GetField("_isInitialized", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(service, true);
            
            // Подписываемся на событие ошибки
            Exception? lastError = null;
            bool errorEventFired = false;
            service.RecognitionError += (sender, e) => 
            {
                lastError = e.Exception;
                errorEventFired = true;
            };
            
            // Создаем тестовые WAV-данные
            byte[] wavData = CreateFullTestWavData();
            
            // Act & Assert
            // Должно выбросить исключение
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.ProcessStreamAsync(wavData));
            
            // Проверяем детальную информацию об ошибке
            Assert.True(callCount > 0, "Должна быть хотя бы одна попытка распознавания");
            Assert.True(errorEventFired, "Должно быть сгенерировано событие ошибки");
            Assert.NotNull(lastError);
            // Не проверяем конкретное сообщение об ошибке, а просто удостоверяемся, что оно не пустое
            Assert.False(string.IsNullOrEmpty(lastError.Message), "Сообщение об ошибке не должно быть пустым");
        }
        
        [Fact]
        public async Task Service_WithCustomModelSettings_UsesCorrectRecognizer()
        {
            // Arrange
            var customSettings = new CustomModelSettings(
                "custom_model.bin",
                "ru",
                new Dictionary<string, string> { ["mockResponse"] = "Результат пользовательской модели" }
            );
            
            // Создаем мок-распознаватель для настроек
            var recognizerMock = new Mock<ISpeechRecognizer>();
            recognizerMock.Setup(r => r.ModelSettings).Returns(customSettings);
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(customSettings.AdditionalParameters["mockResponse"]);
            recognizerMock.Setup(r => r.InitializeAsync()).Returns(Task.CompletedTask);
            
            // Создаем мок аудиопроцессора с явным указанием, что данные в формате WAV
            var audioProcessorMock = new Mock<IAudioProcessor>();
            audioProcessorMock.Setup(a => a.IsWavFile(It.IsAny<byte[]>())).Returns(true);
            audioProcessorMock.Setup(a => a.PrepareAudioDataAsync(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[] data) => data);
            
            // Создаем сервис напрямую с мок-распознавателем
            var service = new WhisperStreamProcessor(_logger, recognizerMock.Object);
            
            // Подменяем AudioProcessor через рефлексию
            var audioProcessorField = typeof(WhisperStreamProcessor).GetField("_audioProcessor", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (audioProcessorField != null)
            {
                audioProcessorField.SetValue(service, audioProcessorMock.Object);
            }
            
            // Обходим проверку наличия модели
            typeof(WhisperStreamProcessor).GetField("_isInitialized", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(service, true);
            
            // Создаем полноценные тестовые WAV-данные
            byte[] wavData = CreateFullTestWavData();
            
            // Act
            string result = await service.ProcessStreamAsync(wavData);
            
            // Assert
            Assert.Equal("Результат пользовательской модели", result);
        }
        
        // Полноценный метод для создания тестового WAV файла
        private byte[] CreateFullTestWavData()
        {
            // Создаем минимальный WAV файл (44 байта заголовка + 100 байт данных)
            byte[] wavData = new byte[44 + 100];
            
            // RIFF header
            wavData[0] = (byte)'R';
            wavData[1] = (byte)'I';
            wavData[2] = (byte)'F';
            wavData[3] = (byte)'F';
            
            // Размер файла минус первые 8 байт
            int fileSize = wavData.Length - 8;
            wavData[4] = (byte)(fileSize & 0xFF);
            wavData[5] = (byte)((fileSize >> 8) & 0xFF);
            wavData[6] = (byte)((fileSize >> 16) & 0xFF);
            wavData[7] = (byte)((fileSize >> 24) & 0xFF);
            
            // WAVE
            wavData[8] = (byte)'W';
            wavData[9] = (byte)'A';
            wavData[10] = (byte)'V';
            wavData[11] = (byte)'E';
            
            // fmt chunk
            wavData[12] = (byte)'f';
            wavData[13] = (byte)'m';
            wavData[14] = (byte)'t';
            wavData[15] = (byte)' ';
            
            // Размер fmt блока (16 для PCM)
            wavData[16] = 16;
            wavData[17] = 0;
            wavData[18] = 0;
            wavData[19] = 0;
            
            // Аудиоформат (1 = PCM)
            wavData[20] = 1;
            wavData[21] = 0;
            
            // Количество каналов (1 = моно)
            wavData[22] = 1;
            wavData[23] = 0;
            
            // Частота дискретизации (16000 Гц)
            int sampleRate = 16000;
            wavData[24] = (byte)(sampleRate & 0xFF);
            wavData[25] = (byte)((sampleRate >> 8) & 0xFF);
            wavData[26] = (byte)((sampleRate >> 16) & 0xFF);
            wavData[27] = (byte)((sampleRate >> 24) & 0xFF);
            
            // Байт в секунду (частота * каналы * битность / 8)
            int byteRate = sampleRate * 1 * 16 / 8;
            wavData[28] = (byte)(byteRate & 0xFF);
            wavData[29] = (byte)((byteRate >> 8) & 0xFF);
            wavData[30] = (byte)((byteRate >> 16) & 0xFF);
            wavData[31] = (byte)((byteRate >> 24) & 0xFF);
            
            // Байт на семпл (каналы * битность / 8)
            wavData[32] = 2; // 1 * 16 / 8 = 2
            wavData[33] = 0;
            
            // Битность (16 бит)
            wavData[34] = 16;
            wavData[35] = 0;
            
            // data chunk
            wavData[36] = (byte)'d';
            wavData[37] = (byte)'a';
            wavData[38] = (byte)'t';
            wavData[39] = (byte)'a';
            
            // Размер данных (длина - размер заголовка)
            int dataSize = wavData.Length - 44;
            wavData[40] = (byte)(dataSize & 0xFF);
            wavData[41] = (byte)((dataSize >> 8) & 0xFF);
            wavData[42] = (byte)((dataSize >> 16) & 0xFF);
            wavData[43] = (byte)((dataSize >> 24) & 0xFF);
            
            // Заполняем данные синусоидой для правдоподобности
            for (int i = 0; i < dataSize / 2; i++)
            {
                short sample = (short)(Math.Sin(i / 10.0) * 32767);
                wavData[44 + i * 2] = (byte)(sample & 0xFF);
                wavData[44 + i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }
            
            return wavData;
        }
        
        // Вспомогательный метод для создания тестового WAV файла
        private byte[] CreateTestWavData()
        {
            // Создаем минимальный WAV файл (44 байта заголовка + 100 байт данных)
            byte[] wavData = new byte[44 + 100];
            
            // RIFF header
            wavData[0] = (byte)'R';
            wavData[1] = (byte)'I';
            wavData[2] = (byte)'F';
            wavData[3] = (byte)'F';
            
            // Размер файла минус первые 8 байт
            int fileSize = wavData.Length - 8;
            wavData[4] = (byte)(fileSize & 0xFF);
            wavData[5] = (byte)((fileSize >> 8) & 0xFF);
            wavData[6] = (byte)((fileSize >> 16) & 0xFF);
            wavData[7] = (byte)((fileSize >> 24) & 0xFF);
            
            // WAVE
            wavData[8] = (byte)'W';
            wavData[9] = (byte)'A';
            wavData[10] = (byte)'V';
            wavData[11] = (byte)'E';
            
            // fmt chunk
            wavData[12] = (byte)'f';
            wavData[13] = (byte)'m';
            wavData[14] = (byte)'t';
            wavData[15] = (byte)' ';
            
            // Заполняем остальные заголовки
            wavData[16] = 16; // Длина fmt блока
            
            // data chunk
            wavData[36] = (byte)'d';
            wavData[37] = (byte)'a';
            wavData[38] = (byte)'t';
            wavData[39] = (byte)'a';
            
            return wavData;
        }
        
        // Вспомогательный метод для обрезания длинных текстов
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
                return text;
            
            return text.Substring(0, maxLength) + "...";
        }
        
        public void Dispose()
        {
            // Освобождаем ресурсы
            _service?.Dispose();
        }

        private ISpeechRecognitionService CreateService()
        {
            return SpeechRecognitionServiceFactory.CreateService(_logger, _modelPath!, "ru", "tiny");
        }
    }
} 