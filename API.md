# API справочник SpeechRecognition

Данный документ содержит подробное описание API системы распознавания речи и примеры его использования.

## Содержание

- [Основные интерфейсы](#основные-интерфейсы)
  - [ISpeechRecognitionService](#ispeechrecognitionservice)
  - [IAudioProcessor](#iaudioprocessor)
  - [ISpeechRecognizer](#ispeechrecognizer)
  - [IModelProvider](#imodelprovider)
  - [IModelSettings](#imodelsettings)
- [События](#события)
  - [События распознавания](#события-распознавания)
  - [События очереди](#события-очереди)
- [Создание сервисов](#создание-сервисов)
  - [SpeechRecognitionServiceFactory](#speechrecognitionservicefactory)
- [Работа с аудио](#работа-с-аудио)
- [Управление очередью распознавания](#управление-очередью-распознавания)
- [Обработка ошибок](#обработка-ошибок)
- [Примеры использования](#примеры-использования)
  - [Базовое использование](#базовое-использование)
  - [Работа с фрагментами](#работа-с-фрагментами)
  - [Асинхронная очередь](#асинхронная-очередь)

## Основные интерфейсы

### ISpeechRecognitionService

Основной интерфейс для взаимодействия с системой распознавания речи.

```csharp
public interface ISpeechRecognitionService : IDisposable, IAsyncDisposable
{
    // События
    event EventHandler<RecognitionEventArgs> RecognitionCompleted;
    event EventHandler<RecognitionEventArgs> RecognitionStarted;
    event EventHandler<FragmentStatusChangedEventArgs> QueueItemStatusChanged;
    event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;
    
    // Методы инициализации и обработки
    Task InitializeAsync();
    Task<string> ProcessStreamAsync(byte[] audioChunk, CancellationToken cancellationToken = default);
    Task<string> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);
    Task<string> ProcessFileInChunksAsync(string filePath, int numChunks, CancellationToken cancellationToken = default);
    
    // Методы управления очередью
    Task<(Guid QueueItemId, Task<string> ResultTask)> EnqueueRecognitionItemAsync(
        byte[] audioChunk, int priority = 0, string metadata = "", bool processImmediately = false);
    bool ChangeItemPriority(Guid itemId, int newPriority);
    bool CancelQueueItem(Guid itemId);
    QueueItem GetQueueItem(Guid itemId);
    IReadOnlyList<QueueItem> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null);
    void PauseQueue();
    void ResumeQueue();
    void ClearQueue(bool cancelProcessing = false);
    void CancelAllOperations();
    bool RemoveQueueItem(Guid itemId);
    int GetQueueItemCount(RecognitionQueueItemStatus? status = null);
    Task ProcessQueueAsync(int maxParallelProcessing = 1, CancellationToken cancellationToken = default);
}
```

#### Статусы очереди распознавания

```csharp
public enum RecognitionQueueItemStatus
{
    Pending,    // Ожидает обработки
    Processing, // Находится в процессе обработки
    Completed,  // Обработка завершена успешно
    Failed,     // Обработка завершена с ошибкой
    Canceled    // Обработка отменена
}
```

#### Информация об элементе очереди

```csharp
public class QueueItem
{
    public Guid Id { get; }                        // Идентификатор элемента
    public RecognitionQueueItemStatus Status { get; } // Статус элемента
    public int Priority { get; }                   // Приоритет обработки
    public DateTime EnqueueTime { get; }           // Время добавления в очередь
    public string Metadata { get; }                // Пользовательские метаданные
    public int AudioDataSize { get; }              // Размер аудиоданных в байтах
}
```

### IAudioProcessor

Интерфейс для обработки и подготовки аудиоданных.

```csharp
public interface IAudioProcessor : IDisposable
{
    Task<byte[]> PrepareAudioDataAsync(byte[] audioData);
    Task<byte[]> PrepareAudioFileAsync(string filePath);
    bool IsWavFormat(byte[] audioData);
    byte[] AddWavHeader(byte[] pcmData, int sampleRate = 16000, int channels = 1, int bitsPerSample = 16);
    byte[][] SplitAudioFile(string filePath, int numChunks);
}
```

### ISpeechRecognizer

Интерфейс для компонентов распознавания речи.

```csharp
public interface ISpeechRecognizer : IDisposable
{
    RecognizerType RecognizerType { get; }
    Task InitializeAsync();
    Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default);
}
```

### IModelProvider

Интерфейс для провайдеров моделей распознавания.

```csharp
public interface IModelProvider
{
    RecognizerType RecognizerType { get; }
    Task<string> EnsureModelExistsAsync(IModelSettings modelSettings);
    bool IsCompatible(IModelSettings modelSettings);
}
```

### IModelSettings

Интерфейс для настроек моделей распознавания.

```csharp
public interface IModelSettings
{
    RecognizerType RecognizerType { get; }
    string ModelPath { get; }
    string Language { get; }
}
```

## События

### События распознавания

```csharp
// Возникает при начале распознавания
public class RecognitionEventArgs : EventArgs
{
    public RecognitionResult Result { get; }
}

// Возникает при ошибке распознавания
public class RecognitionErrorEventArgs : EventArgs
{
    public Exception Exception { get; }
    public Guid SessionId { get; }
    public int FragmentId { get; }
}

// Результат распознавания
public class RecognitionResult
{
    public Guid Id { get; }
    public Guid SessionId { get; }
    public int FragmentId { get; }
    public string Text { get; }
    public DateTime StartTime { get; }
    public DateTime EndTime { get; }
}
```

### События очереди

```csharp
// Изменение статуса элемента очереди
public class FragmentStatusChangedEventArgs : EventArgs
{
    public Guid QueueItemId { get; }
    public RecognitionQueueItemStatus OldStatus { get; }
    public RecognitionQueueItemStatus NewStatus { get; }
    public string Metadata { get; }
}

// Изменение состояния очереди
public class QueueStateChangedEventArgs : EventArgs
{
    public bool IsActive { get; }
    public int PendingCount { get; }
    public int ProcessingCount { get; }
}
```

## Создание сервисов

### SpeechRecognitionServiceFactory

Фабрика для создания сервисов распознавания речи.

```csharp
// Основной метод создания сервиса
public static ISpeechRecognitionService CreateService(
    ILogger logger,
    string modelPath,
    string language = "ru",
    string modelType = "tiny")
    
// Создание сервиса с пользовательскими настройками
public static ISpeechRecognitionService CreateServiceWithSettings(
    ILogger logger,
    IModelSettings modelSettings)
    
// Создание сервиса с политикой восстановления
public static ISpeechRecognitionService CreateServiceWithRetryPolicy(
    ILogger logger,
    string modelPath,
    string language = "ru",
    string modelType = "tiny",
    RetryPolicy retryPolicy = null)
```

#### Пример создания сервиса

```csharp
// Простое создание
var service = SpeechRecognitionServiceFactory.CreateService(
    logger,
    "models/ggml-tiny.bin",
    "ru",
    "tiny"
);

// С пользовательскими настройками
var settings = new WhisperModelSettings("models/ggml-tiny.bin", "ru");
var service = SpeechRecognitionServiceFactory.CreateServiceWithSettings(logger, settings);

// С политикой восстановления
var retryPolicy = RetryPolicyFactory.CreateExponentialBackoff(maxRetryCount: 3);
var service = SpeechRecognitionServiceFactory.CreateServiceWithRetryPolicy(
    logger,
    "models/ggml-tiny.bin",
    "ru",
    "tiny",
    retryPolicy
);
```

## Работа с аудио

Для подготовки аудиоданных используется `AudioProcessor`:

```csharp
// Создание процессора
var audioProcessor = new AudioProcessor();

// Подготовка аудиоданных из файла
byte[] audioData = await audioProcessor.PrepareAudioFileAsync("audioExamples/test.wav");

// Подготовка аудиоданных из байтового массива
byte[] preparedData = await audioProcessor.PrepareAudioDataAsync(rawData);

// Добавление WAV-заголовка к PCM-данным
byte[] wavData = audioProcessor.AddWavHeader(pcmData, sampleRate: 16000);

// Разделение аудиофайла на фрагменты
byte[][] chunks = audioProcessor.SplitAudioFile("audioExamples/test.wav", numChunks: 3);
```

## Управление очередью распознавания

Пример управления очередью:

```csharp
// Добавление фрагмента в очередь
var (id, resultTask) = await service.EnqueueRecognitionItemAsync(
    audioChunk,
    priority: 0,  // Меньшее значение = выше приоритет
    metadata: "fragment-1"
);

// Изменение приоритета фрагмента
service.ChangeItemPriority(id, newPriority: -1);  // Повышаем приоритет

// Получение информации о фрагменте
var item = service.GetQueueItem(id);
Console.WriteLine($"Статус: {item.Status}, приоритет: {item.Priority}");

// Отмена обработки фрагмента
service.CancelQueueItem(id);

// Получение списка фрагментов
var pendingItems = service.GetQueueItems(RecognitionQueueItemStatus.Pending);
var allItems = service.GetQueueItems();

// Управление очередью
service.PauseQueue();  // Приостановка обработки
service.ResumeQueue(); // Возобновление обработки
service.ClearQueue();  // Очистка очереди

// Запуск обработки очереди с параллелизмом
await service.ProcessQueueAsync(maxParallelProcessing: 2);
```

## Обработка ошибок

Для обработки ошибок можно использовать политики повторных попыток:

```csharp
// Создание политики с константной задержкой
var constantPolicy = RetryPolicyFactory.CreateConstantBackoff(
    maxRetryCount: 3,
    delay: TimeSpan.FromSeconds(2)
);

// Создание политики с экспоненциальной задержкой
var expPolicy = RetryPolicyFactory.CreateExponentialBackoff(
    maxRetryCount: 5,
    initialDelay: TimeSpan.FromSeconds(1),
    maxDelay: TimeSpan.FromSeconds(30)
);

// Выполнение операции с повторными попытками
string result = await expPolicy.ExecuteAsync(async (cancellationToken) => {
    return await service.ProcessStreamAsync(audioData, cancellationToken);
});
```

Также для обработки ошибок можно использовать стандартные конструкции языка C#:

```csharp
try
{
    string result = await service.ProcessFileAsync("audioExamples/test.wav");
    Console.WriteLine($"Распознанный текст: {result}");
}
catch (FileNotFoundException ex)
{
    Console.WriteLine($"Файл не найден: {ex.Message}");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"Ошибка инициализации: {ex.Message}");
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка распознавания: {ex.Message}");
}
```

## Примеры использования

### Базовое использование

```csharp
// Создание логгера
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<Program>();

// Создание и инициализация сервиса
using var service = SpeechRecognitionServiceFactory.CreateService(
    logger,
    "models/ggml-tiny.bin",
    "ru"
);
await service.InitializeAsync();

// Подписка на события
service.RecognitionStarted += (sender, e) =>
{
    logger.LogInformation($"Начало распознавания фрагмента #{e.Result.FragmentId}");
};

service.RecognitionCompleted += (sender, e) =>
{
    logger.LogInformation($"Распознавание завершено: {e.Result.Text}");
};

// Распознавание аудиофайла
string result = await service.ProcessFileAsync("audioExamples/test.wav");
Console.WriteLine($"Результат: {result}");
```

### Работа с фрагментами

```csharp
// Создание сервиса
using var service = SpeechRecognitionServiceFactory.CreateService(
    logger,
    "models/ggml-tiny.bin",
    "ru"
);
await service.InitializeAsync();

// Создание процессора аудио
var audioProcessor = new AudioProcessor();

// Разделение файла на фрагменты
byte[][] chunks = audioProcessor.SplitAudioFile("audioExamples/test.wav", 3);

// Обработка фрагментов
string[] results = new string[chunks.Length];
for (int i = 0; i < chunks.Length; i++)
{
    results[i] = await service.ProcessStreamAsync(chunks[i]);
    Console.WriteLine($"Фрагмент {i+1}: {results[i]}");
}

// Объединение результатов
string fullResult = string.Join(" ", results);
Console.WriteLine($"Полный текст: {fullResult}");
```

### Асинхронная очередь

```csharp
// Создание сервиса
using var service = SpeechRecognitionServiceFactory.CreateService(
    logger,
    "models/ggml-tiny.bin",
    "ru"
);
await service.InitializeAsync();

// Создание процессора аудио
var audioProcessor = new AudioProcessor();

// Разделение файла на фрагменты
byte[][] chunks = audioProcessor.SplitAudioFile("audioExamples/test.wav", 5);

// Добавление фрагментов в очередь с разными приоритетами
var tasks = new List<(Guid Id, Task<string> Task)>();
for (int i = 0; i < chunks.Length; i++)
{
    // Инвертируем приоритет, чтобы последние фрагменты обрабатывались первыми
    int priority = chunks.Length - i; 
    var (id, task) = await service.EnqueueRecognitionItemAsync(
        chunks[i],
        priority: priority,
        metadata: $"fragment-{i}"
    );
    tasks.Add((id, task));
}

// Подписка на события изменения статуса
service.QueueItemStatusChanged += (sender, e) =>
{
    logger.LogInformation($"Фрагмент {e.Metadata}: {e.OldStatus} -> {e.NewStatus}");
};

// Запуск обработки с параллелизмом
_ = service.ProcessQueueAsync(maxParallelProcessing: 2);

// Ожидание завершения всех задач
await Task.WhenAll(tasks.Select(t => t.Task));

// Вывод результатов в порядке фрагментов
for (int i = 0; i < tasks.Count; i++)
{
    string result = await tasks[i].Task;
    Console.WriteLine($"Результат фрагмента {i}: {result}");
}
``` 