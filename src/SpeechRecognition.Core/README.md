# Библиотека распознавания речи SpeechRecognition.Core

## Описание

Библиотека предоставляет API для распознавания русской речи с использованием модели Whisper. Поддерживает обработку как целых аудиофайлов, так и отдельных аудиофрагментов в потоковом режиме.

## Основные возможности

- Распознавание речи из аудиофайлов формата WAV
- Потоковое распознавание фрагментов речи
- Асинхронная обработка аудиоданных
- Обработка PCM-данных с автоматическим добавлением WAV-заголовков
- События для отслеживания процесса распознавания
- Управление очередью фрагментов на распознавание

## Использование

### Подключение библиотеки

```csharp
using SpeechRecognition.Core;
using SpeechRecognition.Core.Events;
using SpeechRecognition.Core.Sessions;
```

### Создание экземпляра сервиса

```csharp
// Создание логгера
ILogger logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<ISpeechRecognitionService>();

// Путь к модели Whisper
string modelPath = Path.Combine("models", "ggml-base.bin");

// Создание сервиса распознавания речи через фабрику
using var recognitionService = SpeechRecognitionServiceFactory.CreateService(
    logger,           // Логгер
    modelPath,        // Путь к файлу модели
    "ru",             // Код языка (по умолчанию "ru")
    "base"            // Тип модели (tiny, base)
);
```

### Подписка на события

```csharp
// Подписка на события распознавания
recognitionService.RecognitionStarted += (sender, e) => {
    Console.WriteLine($"Начало распознавания фрагмента {e.Result.FragmentId}");
};

recognitionService.RecognitionCompleted += (sender, e) => {
    Console.WriteLine($"Распознавание завершено: {e.Result.Text}");
};

// Подписка на события изменения статуса элементов очереди
recognitionService.QueueItemStatusChanged += (sender, e) => {
    Console.WriteLine($"Статус элемента {e.ItemId} изменен: {e.OldStatus} -> {e.NewStatus}");
};
```

### Инициализация сервиса

```csharp
// Инициализация сервиса (загрузка модели)
await recognitionService.InitializeAsync();
```

### Распознавание из файла

```csharp
// Распознавание из файла
string filePath = Path.Combine("audioExamples", "example.wav");
string recognizedText = await recognitionService.ProcessFileAsync(filePath);
Console.WriteLine($"Распознанный текст: {recognizedText}");
```

### Распознавание из потока данных

```csharp
// Получение аудиоданных из внешней системы
byte[] audioChunk = GetAudioChunk(); // Ваш метод получения аудиоданных

// Распознавание потока данных
string result = await recognitionService.ProcessStreamAsync(audioChunk);
Console.WriteLine($"Распознанный текст: {result}");
```

### Распознавание файла с разбиением на фрагменты

```csharp
// Распознавание файла с разбиением на 3 фрагмента
string filePath = Path.Combine("audioExamples", "example.wav");
string result = await recognitionService.ProcessFileInChunksAsync(filePath, 3);
```

## Управление очередью фрагментов на распознавание

Библиотека предоставляет возможность управления очередью фрагментов на распознавание, что позволяет контролировать порядок и приоритеты обработки.

### Добавление фрагмента в очередь

```csharp
// Чтение аудиофайла
byte[] audioData = await File.ReadAllBytesAsync("example.wav");

// Добавление в очередь с приоритетом и метаданными
Guid itemId = recognitionService.EnqueueRecognitionItem(
    audioData,       // Аудиоданные (WAV или PCM)
    0,               // Приоритет (меньшее значение - более высокий приоритет)
    "Важный файл"    // Пользовательские метаданные
);

Console.WriteLine($"Фрагмент добавлен в очередь с ID: {itemId}");
```

### Изменение приоритета фрагмента

```csharp
// Повышаем приоритет фрагмента (значение -1 выше чем 0)
bool result = recognitionService.ChangeItemPriority(itemId, -1);
Console.WriteLine($"Приоритет изменен: {result}");
```

### Получение информации о состоянии очереди

```csharp
// Получение информации о конкретном фрагменте
var item = recognitionService.GetQueueItem(itemId);
Console.WriteLine($"Статус: {item.Status}, Приоритет: {item.Priority}");

// Получение списка всех элементов очереди
var allItems = recognitionService.GetQueueItems();
Console.WriteLine($"Всего элементов в очереди: {allItems.Count}");

// Получение списка ожидающих обработки элементов
var pendingItems = recognitionService.GetQueueItems(RecognitionQueueItemStatus.Pending);
Console.WriteLine($"Ожидают обработки: {pendingItems.Count}");

// Получение количества элементов по статусу
int processingCount = recognitionService.GetQueueItemCount(RecognitionQueueItemStatus.Processing);
int completedCount = recognitionService.GetQueueItemCount(RecognitionQueueItemStatus.Completed);
Console.WriteLine($"В обработке: {processingCount}, Завершено: {completedCount}");
```

### Отмена обработки фрагмента

```csharp
// Отмена обработки конкретного фрагмента
bool canceled = recognitionService.CancelQueueItem(itemId);
Console.WriteLine($"Обработка отменена: {canceled}");
```

### Управление очередью в целом

```csharp
// Приостановка обработки очереди
recognitionService.PauseQueue();
Console.WriteLine("Очередь приостановлена");

// Возобновление обработки очереди
recognitionService.ResumeQueue();
Console.WriteLine("Очередь возобновлена");

// Очистка очереди (без отмены текущих операций)
recognitionService.ClearQueue(false);

// Очистка очереди с отменой текущих операций
recognitionService.ClearQueue(true);
```

### Удаление элемента из очереди

```csharp
// Удаление элемента из очереди
bool removed = recognitionService.RemoveQueueItem(itemId);
Console.WriteLine($"Элемент удален: {removed}");
```

## Управление ресурсами

Сервис реализует интерфейс `IDisposable`, поэтому рекомендуется использовать его в конструкции `using`:

```csharp
using var recognitionService = SpeechRecognitionServiceFactory.CreateService(logger, modelPath);
```

## Пример полного использования

См. примеры в директории `Examples`:
- `ExternalIntegrationExample.cs` - базовый пример использования
- `QueueManagementExample.cs` - пример управления очередью распознавания 