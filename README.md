# SpeechRecognition - система автоматического распознавания речи

Система автоматического распознавания речи (ASR) для обработки аудиофрагментов, нарезанных по тишине внешним приложением.

## Оглавление

- [Обзор](#обзор)
- [Возможности](#возможности)
- [Архитектура](#архитектура)
  - [Общая архитектура](#общая-архитектура)
  - [Диаграмма компонентов](#диаграмма-компонентов)
  - [Основные компоненты](#основные-компоненты)
  - [Поток данных](#поток-данных)
- [Начало работы](#начало-работы)
  - [Системные требования](#системные-требования)
  - [Установка](#установка)
  - [Настройка](#настройка)
- [Использование](#использование)
  - [Консольное приложение](#консольное-приложение)
  - [Интеграция в приложения](#интеграция-в-приложения)
  - [Примеры использования](#примеры-использования)
- [API справочник](#api-справочник)
  - [Основные интерфейсы](#основные-интерфейсы)
  - [События](#события)
  - [Создание сервисов](#создание-сервисов)
  - [Работа с аудио](#работа-с-аудио)
  - [Управление очередью распознавания](#управление-очередью-распознавания)
  - [Обработка ошибок](#обработка-ошибок)
  - [Примеры использования API](#примеры-использования-api)
    - [Базовое использование](#базовое-использование)
    - [Работа с фрагментами](#работа-с-фрагментами)
    - [Асинхронная очередь](#асинхронная-очередь)
- [Расширение функциональности](#расширение-функциональности)
  - [Добавление новых моделей распознавания](#добавление-новых-моделей-распознавания)
  - [Пример использования пользовательской модели](#пример-использования-пользовательской-модели)
  - [Настройка политик восстановления](#настройка-политик-восстановления)
- [Тестирование](#тестирование)
  - [Обзор стратегии тестирования](#обзор-стратегии-тестирования)
  - [Типы тестов](#типы-тестов)
  - [Инструменты тестирования](#инструменты-тестирования)
  - [Запуск тестов](#запуск-тестов)
  - [Чек-листы тестирования](#чек-листы-тестирования)
  - [Тестовые кейсы](#тестовые-кейсы)
  - [Покрытие кода тестами](#покрытие-кода-тестами)
  - [Руководство по добавлению новых тестов](#руководство-по-добавлению-новых-тестов)
  - [Мок-объекты и заглушки](#мок-объекты-и-заглушки)
  - [CI/CD и автоматизация тестирования](#cicd-и-автоматизация-тестирования)
- [Соответствие техническому заданию](#соответствие-техническому-заданию)
  - [Общие требования](#общие-требования)
  - [Функциональные требования](#функциональные-требования)
  - [Ключевые требования](#ключевые-требования)
  - [Компоненты системы](#компоненты-системы)
  - [Технический стек](#технический-стек)
  - [Тестирование](#тестирование-1)
  - [Общая оценка соответствия](#общая-оценка-соответствия)

## Обзор

SpeechRecognition - это программный комплекс для автоматического распознавания речи, разработанный для обработки аудиофрагментов, нарезанных по тишине внешним приложением. Система обеспечивает высокую точность распознавания русской речи, поддерживает различные модели распознавания и предоставляет асинхронную обработку аудиоданных.

## Возможности

- Распознавание речи на русском языке с использованием модели Whisper от OpenAI
- Потоковая обработка аудиофрагментов
- Обработка WAV-файлов
- Асинхронная обработка с очередями и приоритетами
- Управление сессиями распознавания
- Механизмы восстановления после ошибок
- Расширяемая архитектура для добавления новых моделей
- Консольный интерфейс для тестирования

## Архитектура

### Общая архитектура

Система построена с использованием объектно-ориентированного подхода и принципов SOLID:

- **Единственная ответственность (SRP)**: Каждый компонент системы имеет четко определенную зону ответственности.
- **Открытость/закрытость (OCP)**: Система спроектирована для расширения без изменения существующего кода.
- **Подстановка Лисков (LSP)**: Все реализации интерфейсов соответствуют контрактам.
- **Разделение интерфейсов (ISP)**: Интерфейсы разделены на логические группы.
- **Инверсия зависимостей (DIP)**: Компоненты высокого уровня не зависят от компонентов низкого уровня.

Основные принципы проектирования:

1. **Модульность**: Система разделена на логические модули с минимальной связностью.
2. **Интерфейсы**: Взаимодействие между компонентами осуществляется через интерфейсы.
3. **Фабрики**: Создание сложных объектов осуществляется через фабрики.
4. **Композиция**: Компоненты системы композируются для создания сложного поведения.
5. **Асинхронность**: Система активно использует асинхронные операции для обработки аудио.

### Диаграмма компонентов

```
+----------------+     +--------------------+     +----------------------+
|  Клиентский    |     |                    |     |                      |
|  код           |<--->| ISpeechRecognition |<--->| Recognition Session  |
|                |     | Service            |     |                      |
+----------------+     +--------------------+     +----------------------+
                               ^                            ^
                               |                            |
                               v                            v
+------------------+     +----------------+     +---------------------+
|                  |     |                |     |                     |
| AudioProcessor   |<--->| ISpeechRecog-  |<--->| SessionManager     |
|                  |     | nizer           |     |                     |
+------------------+     +----------------+     +---------------------+
                                  ^
                                  |
                                  v
                           +---------------+     +-------------------+
                           |               |     |                   |
                           | IModelProvider|<--->| IModelSettings    |
                           |               |     |                   |
                           +---------------+     +-------------------+
```

### Основные компоненты

Система построена на основе модульной архитектуры с четким разделением обязанностей:

1. **AudioProcessor** - обрабатывает и подготавливает аудиоданные для распознавания

   - Подготовка аудиоданных для распознавания
   - Добавление WAV-заголовков к PCM-данным
   - Ресемплирование и преобразование форматов
   - Валидация аудиоданных

2. **Speech Recognizers** - компоненты распознавания речи

   - `WhisperRecognizer` - использует модель Whisper от OpenAI
   - `MockSpeechRecognizer` - реализация для тестирования
   - `CustomSpeechRecognizer` - пользовательская реализация для расширения
   - `BaseSpeechRecognizer` - базовый класс для всех распознавателей

3. **SessionManager** - управляет сессиями распознавания

   - Создание и управление сессиями
   - Управление очередью распознавания
   - Координация работы распознавателей
   - Отслеживание статусов и результатов

4. **Models** - управление моделями распознавания

   - Загрузка моделей
   - Обеспечение доступности моделей
   - Настройка параметров моделей

5. **Recovery** - механизмы восстановления после ошибок

   - Обработка временных сбоев
   - Повторные попытки операций
   - Логирование ошибок

6. **Events** - система событий для оповещения о процессе распознавания
   - Оповещение о ходе распознавания
   - Передача результатов распознавания
   - Уведомление об ошибках
   - Отслеживание статусов

### Поток данных

#### Обработка аудиофайла

1. Клиент вызывает `ISpeechRecognitionService.ProcessFileAsync`
2. Сервис использует `AudioProcessor` для чтения и подготовки файла
3. Создается сессия через `SessionManager`
4. Аудиоданные передаются в `ISpeechRecognizer`
5. Распознаватель использует модель для распознавания
6. Результат возвращается клиенту
7. Генерируется событие `RecognitionCompleted`

#### Обработка потоковых данных

1. Клиент вызывает `ISpeechRecognitionService.ProcessStreamAsync`
2. Аудиоданные проверяются и подготавливаются с помощью `AudioProcessor`
3. Сервис передает данные в распознаватель
4. Распознавание выполняется асинхронно
5. Результат возвращается клиенту
6. Генерируется событие `RecognitionCompleted`

#### Работа с очередью распознавания

1. Клиент добавляет фрагменты через `EnqueueRecognitionItemAsync`
2. Каждый фрагмент получает уникальный идентификатор и TaskCompletionSource
3. Клиент запускает обработку очереди через `ProcessQueueAsync`
4. `SessionManager` обрабатывает элементы в порядке приоритета
5. Статус фрагментов изменяется (Pending → Processing → Completed/Failed)
6. Генерируются события `QueueItemStatusChanged`
7. По завершении обработки результаты доступны через результаты соответствующих задач

## Начало работы

### Системные требования

- **.NET 9.0**
- Windows, Linux или macOS
- Минимум 4 ГБ оперативной памяти (рекомендуется 8 ГБ+)

### Установка

1. Клонируйте репозиторий:

   ```
   git clone https://github.com/RusakovRodion/ASR.git
   ```

2. Перейдите в директорию проекта:

   ```
   cd ASR
   ```

3. Соберите проект:

   ```
   dotnet build src/SpeechRecognition.sln
   ```

4. Проверьте, что в директории `models/` находятся модели для распознавания:

   - `ggml-tiny.bin` (меньше и быстрее, но менее точная)
   - `ggml-base.bin` (более точная, но требует больше ресурсов)

   Если моделей нет, их можно скачать с официального репозитория Whisper или использовать предустановленные модели.

### Запуск консольного приложения

Есть несколько способов запустить консольное приложение:

#### Способ 1: Использование dotnet run

```
dotnet run --project src/SpeechRecognition.Console/SpeechRecognition.Console.csproj -- --file audioExamples/test.wav --language ru
```

#### Способ 2: Использование готового исполняемого файла

В репозитории уже есть собранное приложение в директории `publish-self-contained`. Вы можете запустить его напрямую:

```
publish-self-contained/whisper-stream.exe --file audioExamples/test.wav --language ru
```

#### Параметры командной строки:

- `--file` или `-f`: Путь к аудиофайлу для распознавания
- `--language` или `-l`: Язык для распознавания (по умолчанию: ru)
- `--model` или `-m`: Путь к файлу модели Whisper (по умолчанию: models/ggml-base.bin)
- `--num-chunks` или `-n`: Количество фрагментов для обработки (по умолчанию: 1)

#### Примеры использования:

```bash
# Базовое распознавание русской речи из файла test.wav
whisper-stream --file audioExamples/test.wav --language ru

# Распознавание с разбиением на 3 фрагмента
whisper-stream --file audioExamples/test.wav --language ru --num-chunks 3

# Использование конкретной модели tiny
whisper-stream --file audioExamples/test.wav --language ru --model models/ggml-tiny.bin
```

### Запуск тестов

Для запуска тестов используйте следующую команду:

```
dotnet test SpeechRecognition.Tests/SpeechRecognition.Tests.csproj
```

Для запуска тестов с измерением покрытия кода:

```
dotnet test SpeechRecognition.Tests/SpeechRecognition.Tests.csproj /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Для запуска определенной категории тестов:

```
dotnet test SpeechRecognition.Tests/SpeechRecognition.Tests.csproj --filter "Category=Integration"
```

## Использование

### Интеграция в приложения

Для интеграции в существующие приложения используйте библиотеку SpeechRecognition.Core:

1. Добавьте ссылку на проект или создайте NuGet-пакет из проекта SpeechRecognition.Core:

   ```
   dotnet add reference <путь_к_проекту>/src/SpeechRecognition.Core/SpeechRecognition.Core.csproj
   ```

2. Пример кода для интеграции:

```csharp
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core;

// Создание логгера
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<Program>();

// Создание и инициализация сервиса распознавания
using var service = SpeechRecognitionServiceFactory.CreateService(
    logger,
    "models/ggml-tiny.bin", // Путь к модели
    "ru",                   // Язык распознавания
    "tiny"                  // Тип модели
);

// Инициализация сервиса (загрузка модели)
await service.InitializeAsync();

// Подписка на события распознавания
service.RecognitionStarted += (sender, e) => {
    Console.WriteLine($"Начало распознавания фрагмента #{e.Result.FragmentId}");
};

service.RecognitionCompleted += (sender, e) => {
    Console.WriteLine($"Распознавание завершено: {e.Result.Text}");
};

// Обработка аудиофайла
string result = await service.ProcessFileAsync("path/to/audio.wav");
Console.WriteLine($"Результат распознавания: {result}");

// Обработка аудиофрагмента из потока байтов
byte[] audioChunk = GetAudioChunk(); // получение аудиоданных
string fragmentResult = await service.ProcessStreamAsync(audioChunk);
Console.WriteLine($"Результат фрагмента: {fragmentResult}");

// Пример работы с очередью фрагментов
var (queueItemId, resultTask) = await service.EnqueueRecognitionItemAsync(
    audioChunk,
    priority: 0, // Меньшее значение = выше приоритет
    metadata: "fragment1"
);

// Запуск обработки очереди с параллелизмом
await service.ProcessQueueAsync(maxParallelProcessing: 2);

// Получение результата
string text = await resultTask;
Console.WriteLine($"Результат из очереди: {text}");
```

### Полный пример интеграции

Пример приложения, использующего систему распознавания речи:

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core;

namespace SpeechRecognitionDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Настройка логгера
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            // Путь к модели и аудиофайлам
            string modelPath = "models/ggml-tiny.bin";
            string audioPath = "audio/example.wav";

            // Проверяем наличие файлов
            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Модель не найдена: {modelPath}");
                return;
            }

            if (!File.Exists(audioPath))
            {
                Console.WriteLine($"Аудиофайл не найден: {audioPath}");
                return;
            }

            try
            {
                // Создание и инициализация сервиса распознавания
                using var service = SpeechRecognitionServiceFactory.CreateService(
                    logger, modelPath, "ru", "tiny");

                // Подписка на события
                service.RecognitionStarted += (sender, e) => {
                    Console.WriteLine($"Начало распознавания фрагмента #{e.Result.FragmentId}");
                };

                service.RecognitionCompleted += (sender, e) => {
                    Console.WriteLine($"Распознавание завершено: {e.Result.Text}");
                    Console.WriteLine($"Длительность: {(e.Result.EndTime - e.Result.StartTime).TotalSeconds:F2} секунд");
                };

                service.QueueItemStatusChanged += (sender, e) => {
                    Console.WriteLine($"Статус фрагмента {e.Metadata}: {e.OldStatus} -> {e.NewStatus}");
                };

                // Инициализация сервиса
                Console.WriteLine("Инициализация модели...");
                await service.InitializeAsync();
                Console.WriteLine("Модель инициализирована успешно");

                // Распознавание целого файла
                Console.WriteLine($"Распознавание файла: {audioPath}");
                string result = await service.ProcessFileAsync(audioPath);
                Console.WriteLine($"Результат распознавания:\n{result}");

                // Распознавание файла по частям
                Console.WriteLine("\nРаспознавание файла по частям (3 фрагмента):");
                string chunkResult = await service.ProcessFileInChunksAsync(audioPath, numChunks: 3);
                Console.WriteLine($"Результат распознавания по частям:\n{chunkResult}");

                Console.WriteLine("\nПример завершен успешно");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
                }
            }
        }
    }
}
```

### Примеры использования

Проверка доступности модели и распознавание аудиофайла:

```csharp
using SpeechRecognition.Core;
using Microsoft.Extensions.Logging;

// Создание логгера
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<Program>();

// Создание и инициализация процессора
using var processor = new WhisperStreamProcessor(logger, "models/ggml-tiny.bin", "ru");
await processor.InitializeAsync();

// Обработка файла
var result = await processor.ProcessFileAsync("audioExamples/test.wav");
Console.WriteLine($"Распознанный текст: {result}");
```

## API справочник

Данный раздел содержит подробное описание API системы распознавания речи и примеры его использования.

### Основные интерфейсы

#### ISpeechRecognitionService

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

##### Статусы очереди распознавания

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

##### Информация об элементе очереди

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

#### IAudioProcessor

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

#### ISpeechRecognizer

Интерфейс для компонентов распознавания речи.

```csharp
public interface ISpeechRecognizer : IDisposable
{
    RecognizerType RecognizerType { get; }
    Task InitializeAsync();
    Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default);
}
```

#### IModelProvider и IModelSettings

```csharp
public interface IModelProvider
{
    RecognizerType RecognizerType { get; }
    Task<string> EnsureModelExistsAsync(IModelSettings modelSettings);
    bool IsCompatible(IModelSettings modelSettings);
}

public interface IModelSettings
{
    RecognizerType RecognizerType { get; }
    string ModelPath { get; }
    string Language { get; }
}
```

### События

#### События распознавания

```csharp
// Возникает при начале/завершении распознавания
public class RecognitionEventArgs : EventArgs
{
    public RecognitionResult Result { get; }
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

#### События очереди

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

### Создание сервисов

#### SpeechRecognitionServiceFactory

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

### Работа с аудио

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

### Управление очередью распознавания

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

### Обработка ошибок

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

### Примеры использования API

#### Базовое использование

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

#### Работа с фрагментами

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

#### Асинхронная очередь

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

## Расширение функциональности

Система поддерживает расширение для работы с различными моделями распознавания речи через систему абстракций.

### Добавление новых моделей распознавания

Для добавления новой модели необходимо:

#### 1. Создание настроек модели

Создайте класс настроек, реализующий интерфейс `IModelSettings`:

```csharp
public class MyModelSettings : IModelSettings
{
    // Тип распознавателя (добавьте соответствующее значение в RecognizerType)
    public RecognizerType RecognizerType => RecognizerType.Custom; // или ваш собственный тип

    // Путь к файлу модели
    public string ModelPath { get; }

    // Код языка для распознавания
    public string Language { get; }

    // Дополнительные свойства для вашей модели
    public bool UseGPU { get; }
    public int BeamSize { get; }

    // Конструктор
    public MyModelSettings(string modelPath, string language = "ru", bool useGPU = false, int beamSize = 5)
    {
        ModelPath = modelPath;
        Language = string.IsNullOrEmpty(language) ? "ru" : language;
        UseGPU = useGPU;
        BeamSize = beamSize;
    }
}
```

#### 2. Создание провайдера модели

Создайте класс провайдера модели, реализующий интерфейс `IModelProvider`:

```csharp
public class MyModelProvider : IModelProvider
{
    private readonly ILogger _logger;

    public RecognizerType RecognizerType => RecognizerType.Custom; // Или ваш собственный тип

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

        var mySettings = (MyModelSettings)modelSettings;

        // Проверяем существование файла модели
        if (File.Exists(mySettings.ModelPath))
        {
            _logger.LogInformation($"Модель найдена: {mySettings.ModelPath}");
            return mySettings.ModelPath;
        }

        // Загрузка модели (если требуется)
        // ...

        return mySettings.ModelPath;
    }

    public bool IsCompatible(IModelSettings modelSettings)
    {
        return modelSettings is MyModelSettings && modelSettings.RecognizerType == RecognizerType.Custom;
    }
}
```

#### 3. Создание распознавателя

Создайте класс распознавателя, наследующийся от `BaseSpeechRecognizer`:

```csharp
public class MyModelRecognizer : BaseSpeechRecognizer
{
    private readonly ILogger _logger;
    private MyModelSettings _mySettings;
    private MyModelImplementation _model; // Ваша реализация модели

    public override RecognizerType RecognizerType => RecognizerType.Custom; // Или ваш собственный тип

    public MyModelRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings, ILogger logger)
        : base(audioProcessor, modelSettings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (modelSettings is not MyModelSettings)
        {
            throw new ArgumentException("Требуются настройки вашей модели", nameof(modelSettings));
        }

        _mySettings = (MyModelSettings)modelSettings;
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
            // Инициализация вашей модели
            _model = new MyModelImplementation(_mySettings.ModelPath);
            _model.SetLanguage(_mySettings.Language);
            _model.UseGPU = _mySettings.UseGPU;
            _model.BeamSize = _mySettings.BeamSize;

            await _model.InitializeAsync();

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при инициализации модели");
            throw new InvalidOperationException("Ошибка при инициализации модели", ex);
        }
    }

    public override async Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Подготавливаем аудиоданные
        byte[] preparedAudioData = await _audioProcessor.PrepareAudioDataAsync(audioData);

        // Распознавание речи с вашей моделью
        using (var stream = new MemoryStream(preparedAudioData))
        {
            var result = await _model.RecognizeAsync(stream, cancellationToken);
            return result;
        }
    }

    public override void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _model?.Dispose();
        _model = null;

        _isDisposed = true;
        GC.SuppressFinalize(this);
    }
}
```

#### 4. Регистрация в фабриках

Обновите фабрики для поддержки вашей модели:

```csharp
// В ModelProviderFactory.cs
public static IModelProvider CreateProvider(RecognizerType recognizerType, ILogger logger)
{
    // ...
    return recognizerType switch
    {
        RecognizerType.Whisper => new WhisperModelProvider(logger),
        RecognizerType.Mock => new MockModelProvider(logger),
        RecognizerType.Custom => new CustomModelProvider(logger),
        RecognizerType.MyModel => new MyModelProvider(logger), // Ваш провайдер
        _ => throw new ArgumentException($"Неподдерживаемый тип распознавателя: {recognizerType}")
    };
}

// В SpeechRecognizerFactory.cs
public static ISpeechRecognizer CreateRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings, ILogger logger)
{
    // ...
    return modelSettings.RecognizerType switch
    {
        // ...
        RecognizerType.MyModel => modelSettings is MyModelSettings
            ? new MyModelRecognizer(audioProcessor, modelSettings, logger)
            : throw new ArgumentException("Неверный тип настроек", nameof(modelSettings)),
        _ => throw new ArgumentException($"Неподдерживаемый тип распознавателя: {modelSettings.RecognizerType}")
    };
}
```

### Пример использования пользовательской модели

```csharp
// Создаем логгер
using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<Program>();

// Вариант 1: Использование CustomModelSettings
var customSettings = new CustomModelSettings("path/to/model.bin", "ru",
    new Dictionary<string, string> { ["param1"] = "value1", ["param2"] = "value2" });
var recognizer = SpeechRecognitionServiceFactory.CreateService(logger, customSettings);

// Вариант 2: Использование специального метода
var myModelService = SpeechRecognitionServiceFactory.CreateCustomService(
    logger,
    "path/to/model.bin",
    "ru",
    new Dictionary<string, string> { ["param1"] = "value1" }
);

// Инициализация и использование
await myModelService.InitializeAsync();
string result = await myModelService.ProcessFileAsync("audio.wav");
Console.WriteLine(result);
```

### Настройка политик восстановления

Система поддерживает различные политики повторных попыток для обработки временных сбоев:

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

// Настройка сервиса с политикой восстановления
var service = SpeechRecognitionServiceFactory.CreateServiceWithRetryPolicy(
    logger,
    modelPath,
    language: "ru",
    retryPolicy: expPolicy
);
```

Это позволяет адаптировать поведение системы к различным сценариям использования и условиям работы.

## Тестирование

### Обзор стратегии тестирования

Стратегия тестирования системы SpeechRecognition направлена на обеспечение высокого качества и надежности распознавания речи. Тестирование охватывает все основные компоненты системы, начиная от обработки аудио и заканчивая распознаванием речи и управлением очередью.

**Основные принципы стратегии тестирования:**

1. **Многоуровневое тестирование** - от модульных тестов отдельных компонентов до интеграционного тестирования всей системы.
2. **Автоматизация** - максимальное количество тестов должно выполняться автоматически.
3. **Изоляция** - компоненты тестируются изолированно с использованием моков и заглушек.
4. **Регрессионное тестирование** - регулярное выполнение всех тестов для предотвращения регрессий.
5. **Тестирование восстановления** - проверка способности системы восстанавливаться после сбоев.
6. **Тестирование производительности** - оценка производительности распознавания речи.

### Типы тестов

#### Модульные тесты

Модульные тесты проверяют функциональность отдельных компонентов системы в изоляции от других частей.

**Основные модульные тесты:**

- **AudioProcessorTests** - тесты для компонента обработки аудио
- **RecognizerTests** - тесты для распознавателей речи
- **ModelExtensionTests** - тесты для расширений моделей
- **SessionManagerTests** - тесты для менеджера сессий

**Пример модульного теста:**

```csharp
[Fact]
public async Task AddWavHeader_AddsCorrectHeaderToPcmData()
{
    // Arrange
    var audioProcessor = new AudioProcessor();
    byte[] pcmData = GenerateFakePcmData(1000);

    // Act
    byte[] wavData = audioProcessor.AddWavHeader(pcmData);

    // Assert
    Assert.True(audioProcessor.IsWavFormat(wavData));
    Assert.Equal(pcmData.Length + 44, wavData.Length); // WAV заголовок = 44 байта
}
```

#### Интеграционные тесты

Интеграционные тесты проверяют взаимодействие между различными компонентами системы.

**Основные интеграционные тесты:**

- **SpeechRecognitionServiceTests** - тесты для сервиса распознавания речи
- **IntegrationTests** - общие интеграционные тесты системы

**Пример интеграционного теста:**

```csharp
[Fact]
public async Task ProcessFileAsync_ValidFile_ReturnsRecognizedText()
{
    // Пропускаем тест, если нет аудиофайла или модели
    if (!_hasTestWavFile || !_hasModel || _service == null)
    {
        return;
    }

    // Arrange
    await _service.InitializeAsync();

    // Act
    string result = await _service.ProcessFileAsync(_testWavPath!);

    // Assert
    Assert.NotNull(result);
    Assert.NotEmpty(result);
}
```

#### Тесты восстановления

Тесты восстановления проверяют способность системы восстанавливаться после ошибок и сбоев.

**Пример теста восстановления:**

```csharp
[Fact]
public async Task RetryPolicy_RetriesToExecuteOperation()
{
    // Arrange
    int attemptsCount = 0;
    var policy = RetryPolicyFactory.CreateConstantBackoff(
        maxRetryCount: 3,
        delay: TimeSpan.FromMilliseconds(10)
    );

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(async () =>
    {
        await policy.ExecuteAsync(cancellationToken =>
        {
            attemptsCount++;
            throw new InvalidOperationException("Test exception");
        });
    });

    // Проверяем, что было выполнено 4 попытки (1 основная + 3 повторных)
    Assert.Equal(4, attemptsCount);
}
```

#### Тесты производительности

Тесты производительности оценивают скорость и эффективность распознавания речи.

**Основные тесты производительности:**

- Тесты скорости распознавания речи
- Тесты параллельной обработки аудиофрагментов
- Тесты масштабируемости

### Инструменты тестирования

Для тестирования системы SpeechRecognition используются следующие инструменты:

1. **xUnit** - фреймворк для модульного тестирования
2. **Moq** - библиотека для создания мок-объектов
3. **FluentAssertions** - библиотека для создания читаемых утверждений
4. **BenchmarkDotNet** - инструмент для проведения микробенчмарков
5. **coverlet** - инструмент для измерения покрытия кода тестами

### Запуск тестов

#### Запуск всех тестов

Для запуска всех тестов используйте команду:

```
dotnet test
```

#### Запуск отдельной категории тестов

Для запуска определенной категории тестов:

```
dotnet test --filter "Category=Integration"
```

Доступные категории:

- Unit
- Integration
- Recovery
- Performance

#### Настройка параметров тестирования

Тесты могут быть настроены через файл конфигурации `testsettings.json`:

```json
{
  "TestSettings": {
    "ModelPath": "models/ggml-tiny.bin",
    "AudioExamplesPath": "audioExamples",
    "Language": "ru",
    "PerformanceTestTimeoutSeconds": 30
  }
}
```

### Чек-листы тестирования

#### Чек-лист для обработки аудио

- [x] Проверка добавления WAV-заголовка к PCM-данным
- [x] Проверка определения формата аудиофайла
- [x] Проверка ресемплирования аудио
- [x] Проверка разделения аудиофайла на фрагменты
- [x] Проверка подготовки аудиоданных для распознавания
- [x] Проверка обработки некорректных аудиоданных

#### Чек-лист для распознавания речи

- [x] Проверка распознавания речи из WAV-файла
- [x] Проверка распознавания речи из PCM-данных
- [x] Проверка инициализации моделей
- [x] Проверка корректного освобождения ресурсов
- [x] Проверка распознавания речи с различными языками
- [x] Проверка обработки некорректных входных данных

#### Чек-лист для управления сессиями

- [x] Проверка создания сессии
- [x] Проверка добавления фрагментов в очередь
- [x] Проверка изменения приоритета фрагментов
- [x] Проверка отмены обработки фрагментов
- [x] Проверка паузы и возобновления обработки
- [x] Проверка очистки очереди
- [x] Проверка параллельной обработки фрагментов

### Тестовые кейсы

В проекте реализовано 66 тестовых кейсов, охватывающих различные аспекты функциональности.

#### Тесты обработки аудио (AudioProcessorTests)

| ID    | Название теста                                   | Описание                                                   |
| ----- | ------------------------------------------------ | ---------------------------------------------------------- |
| AP-01 | ProcessWavFile_ValidFile_ReturnsProcessedData    | Проверка корректной обработки WAV-файла                    |
| AP-02 | AddWavHeader_PcmData_ReturnsValidWavData         | Проверка корректного добавления WAV-заголовка к PCM-данным |
| AP-03 | ProcessAudio_InvalidFormat_ThrowsException       | Проверка обработки некорректного формата аудиоданных       |
| AP-04 | ProcessAudio_DifferentLengths_ProcessesCorrectly | Проверка обработки аудиофрагментов разной длины            |
| AP-05 | ProcessDifferentFileFormats_HandlesCorrectly     | Проверка обработки разных форматов аудиофайлов             |

#### Тесты распознавания речи (RecognizerTests)

| ID    | Название теста                                    | Описание                                         |
| ----- | ------------------------------------------------- | ------------------------------------------------ |
| RC-01 | RecognizeSpeech_ValidAudio_ReturnsText            | Проверка распознавания текста из валидного аудио |
| RC-02 | RecognizeSpeech_RussianSpeech_ReturnsCorrectText  | Проверка распознавания русской речи              |
| RC-03 | RecognizeSpeech_ShortAudio_ProcessesCorrectly     | Проверка распознавания короткого аудиофрагмента  |
| RC-04 | RecognizeSpeech_LongAudio_ProcessesCorrectly      | Проверка распознавания длинного аудиофрагмента   |
| RC-05 | RecognizeSpeech_InvalidAudio_ThrowsException      | Проверка обработки некорректных аудиоданных      |
| RC-06 | RecognizeSpeech_WithCancellation_CancelsOperation | Проверка отмены операции распознавания           |
| RC-07 | RecognizeMultipleWavFiles_ProcessesCorrectly      | Проверка распознавания нескольких WAV-файлов     |

#### Тесты управления сессиями (SessionManagerTests)

| ID    | Название теста                                                           | Описание                                                |
| ----- | ------------------------------------------------------------------------ | ------------------------------------------------------- |
| SM-01 | CreateSession_ReturnsNewSession                                          | Проверка создания новой сессии                          |
| SM-02 | GetSession_ReturnsCorrectSession                                         | Проверка получения существующей сессии                  |
| SM-03 | GetAllSessions_ReturnsAllSessions                                        | Проверка получения всех сессий                          |
| SM-04 | GetFragments_ReturnsCorrectFragments                                     | Проверка получения фрагментов сессии                    |
| SM-05 | PauseAndResumeQueue_ChangesQueueState                                    | Проверка паузы и возобновления очереди                  |
| SM-06 | EnqueueFragmentAsync_WithImmediateProcessing_StartsProcessingImmediately | Проверка немедленной обработки фрагмента                |
| SM-07 | EnqueueFragmentAsync_WithDifferentPriorities_ProcessesHighPriorityFirst  | Проверка обработки фрагментов в порядке приоритета      |
| SM-08 | GetFragmentGlobalId_ReturnsValidId                                       | Проверка получения глобального идентификатора фрагмента |
| SM-09 | CancelTokenMiddleOfProcessing_AbortsFurtherProcessing                    | Проверка отмены обработки во время выполнения           |
| SM-10 | QueueProcessing_WithRetryFailedItems_RetriesFailedFragments              | Проверка повторной обработки неудачных фрагментов       |
| SM-11 | RetryFailedFragment_WithCorrectSessionAndFragmentIds_ReturnsTrue         | Проверка повторной обработки неудачного фрагмента по ID |

#### Тесты восстановления (RecoveryTests)

| ID    | Название теста                                                    | Описание                                                     |
| ----- | ----------------------------------------------------------------- | ------------------------------------------------------------ |
| RV-01 | RetryPolicy_BasicOperation_SucceedsWithoutRetries                 | Проверка успешного выполнения без повторных попыток          |
| RV-02 | RetryPolicy_FailureWithRetry_EventualSuccess                      | Проверка успешного выполнения после повторных попыток        |
| RV-03 | RetryPolicy_MaxRetryExceeded_ThrowsException                      | Проверка превышения максимального числа повторных попыток    |
| RV-04 | RetryPolicy_NonRetryableException_NoRetries                       | Проверка отсутствия повторов для необрабатываемых исключений |
| RV-05 | RetryPolicy_Cancellation_NoRetries                                | Проверка отсутствия повторов при отмене операции             |
| RV-06 | RetryPolicy_ExponentialBackoff_IncreasesDelays                    | Проверка экспоненциального увеличения задержек               |
| RV-07 | SpeechRecognitionService_WithRetryPolicy_HandlesTemporaryFailures | Проверка обработки временных ошибок с политикой повторов     |

#### Тесты расширяемости модели (ModelExtensionTests)

| ID    | Название теста                                               | Описание                                                          |
| ----- | ------------------------------------------------------------ | ----------------------------------------------------------------- |
| ME-01 | CustomModelSettings_StoreParameters_Correctly                | Проверка корректного хранения параметров пользовательской модели  |
| ME-02 | CustomModelProvider_IsCompatible_WithCustomModelSettings     | Проверка совместимости провайдера с настройками модели            |
| ME-03 | CustomModelProvider_EnsureModelExists_ChecksFilePath         | Проверка проверки пути к файлу модели                             |
| ME-04 | CustomModelProvider_EnsureModelExists_ThrowsIfFileNotFound   | Проверка исключения при отсутствии файла модели                   |
| ME-05 | MyModelSettings_StoresProperties_Correctly                   | Проверка хранения свойств пользовательской модели                 |
| ME-06 | MyModelProvider_IsCompatible_WithMyModelSettings             | Проверка совместимости провайдера с настройками модели            |
| ME-07 | MyModelRecognizer_InitializeAndRecognize_UsesMyModelSettings | Проверка использования настроек при инициализации и распознавании |
| ME-08 | MyModelRecognizer_Dispose_ReleasesResources                  | Проверка освобождения ресурсов при уничтожении распознавателя     |

#### Тесты сервиса распознавания (SpeechRecognitionServiceTests)

| ID     | Название теста                                         | Описание                                                                          |
| ------ | ------------------------------------------------------ | --------------------------------------------------------------------------------- |
| SRS-01 | InitializeAsync_Succeeds                               | Проверка успешной инициализации сервиса                                           |
| SRS-02 | ProcessStreamAsync_ValidAudio_ReturnsText              | Проверка обработки потока аудиоданных                                             |
| SRS-03 | ProcessFileAsync_ValidFile_ReturnsText                 | Проверка обработки аудиофайла                                                     |
| SRS-04 | ProcessFileInChunksAsync_ValidFile_ReturnsText         | Проверка обработки файла по фрагментам                                            |
| SRS-05 | ProcessStream_InvalidAudio_ThrowsException             | Проверка обработки некорректного потока аудиоданных                               |
| SRS-06 | ProcessFile_NonExistentFile_ThrowsException            | Проверка обработки несуществующего файла                                          |
| SRS-07 | CancellationToken_CancelsOperation                     | Проверка отмены операции распознавания                                            |
| SRS-08 | ProcessMultipleAudioFiles_ReturnsValidResults          | Проверка обработки нескольких аудиофайлов                                         |
| SRS-09 | ProcessMultipleStreamsInParallel_ReturnsCorrectResults | Проверка параллельной обработки нескольких потоков                                |
| SRS-10 | EnqueueRecognitionItemAsync_ProcessesItemsWithPriority | Проверка обработки элементов очереди с учетом приоритета                          |
| SRS-11 | CancelQueueItem_CancelsProcessing                      | Проверка отмены обработки элемента очереди                                        |
| SRS-12 | PauseAndResumeQueue_ControlsProcessing                 | Проверка паузы и возобновления обработки очереди                                  |
| SRS-13 | ClearQueue_RemovesAllPendingItems                      | Проверка очистки очереди                                                          |
| SRS-14 | CancelAllOperations_CancelsAllActiveOperations         | Проверка отмены всех активных операций                                            |
| SRS-15 | CancelProcessingDuringRecognition_CancelsOperation     | Проверка отмены процесса распознавания во время выполнения                        |
| SRS-16 | Service_WithRetryPolicy_RetriesFailingOperations       | Проверка повторных попыток при сбойных операциях                                  |
| SRS-17 | Service_WithMultipleFailures_ReportsDetailedErrorInfo  | Проверка детальной информации об ошибках при множественных сбоях                  |
| SRS-18 | Service_WithCustomModelSettings_UsesCorrectRecognizer  | Проверка использования правильного распознавателя с пользовательскими настройками |

#### Интеграционные тесты (IntegrationTests)

| ID    | Название теста                                       | Описание                                                  |
| ----- | ---------------------------------------------------- | --------------------------------------------------------- |
| IT-01 | CompleteRecognitionPipeline_ValidAudio_Succeeds      | Проверка полного цикла распознавания с валидным аудио     |
| IT-02 | StreamRecognitionPipeline_ValidAudioChunks_Succeeds  | Проверка распознавания потоковых аудиофрагментов          |
| IT-03 | RecognitionWithDifferentFragmentSizes_Succeeds       | Проверка распознавания фрагментов разного размера         |
| IT-04 | ErrorHandlingPipeline_InvalidAudio_HandlesGracefully | Проверка корректной обработки ошибок при невалидном аудио |
| IT-05 | RecognitionWithRussianSpeech_ReturnsCorrectText      | Проверка распознавания русской речи                       |
| IT-06 | RecoveryPolicy_IntegratedWithService_HandlesErrors   | Проверка интеграции политики восстановления с сервисом    |
| IT-07 | CustomModel_IntegrationWithService_Works             | Проверка интеграции пользовательской модели с сервисом    |

### Покрытие кода тестами

Для измерения покрытия кода тестами используется инструмент coverlet. Цель проекта - достичь не менее 80% покрытия кода тестами.

Для запуска тестов с измерением покрытия:

```
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

Для анализа покрытия можно использовать инструмент ReportGenerator:

```
reportgenerator -reports:coverage.opencover.xml -targetdir:coverage_report
```

### Руководство по добавлению новых тестов

При добавлении новых функций в систему необходимо также добавлять соответствующие тесты:

1. **Модульные тесты** для проверки функциональности отдельных компонентов
2. **Интеграционные тесты** для проверки взаимодействия компонентов
3. **Тесты восстановления** для проверки обработки ошибок

#### Шаблон модульного теста

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var component = new Component();

    // Act
    var result = await component.MethodAsync();

    // Assert
    Assert.Equal(expectedValue, result);
}
```

#### Шаблон интеграционного теста

```csharp
[Fact]
public async Task ComponentInteraction_Scenario_ExpectedResult()
{
    // Arrange
    var component1 = new Component1();
    var component2 = new Component2(component1);

    // Act
    var result = await component2.UseComponent1Async();

    // Assert
    Assert.Equal(expectedValue, result);
}
```

### Мок-объекты и заглушки

Для изоляции компонентов при тестировании используются мок-объекты и заглушки:

```csharp
// Пример использования Moq для создания мок-объекта
var mockRecognizer = new Mock<ISpeechRecognizer>();
mockRecognizer.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
    .ReturnsAsync("Тестовый результат распознавания");

var service = new SpeechRecognitionService(mockRecognizer.Object, mockAudioProcessor.Object, mockLogger.Object);
```

## Соответствие техническому заданию

В данном разделе проанализировано соответствие разработанной системы требованиям, указанным в техническом задании.

### Общие требования

| Требование                                                            | Статус       | Комментарий                                                                                        |
| --------------------------------------------------------------------- | ------------ | -------------------------------------------------------------------------------------------------- |
| Автоматическое распознавание речи (ASR) для обработки аудиофрагментов | ✅ Выполнено | Разработана полноценная система распознавания речи с поддержкой обработки аудиофрагментов          |
| Обеспечение точного распознавания русской речи                        | ✅ Выполнено | Система успешно распознает русскую речь, что подтверждено тестами (RC-02, IT-05)                   |
| Гибкость выбора моделей распознавания                                 | ✅ Выполнено | Реализована расширяемая архитектура с поддержкой различных моделей (см. тесты ModelExtensionTests) |
| Простая интеграция с внешними приложениями                            | ✅ Выполнено | Предоставлено API для интеграции и примеры использования                                           |
| Асинхронная обработка аудиоданных                                     | ✅ Выполнено | Реализована полноценная асинхронная обработка с поддержкой очередей                                |

### Функциональные требования

| Требование                                                  | Статус       | Комментарий                                                                       |
| ----------------------------------------------------------- | ------------ | --------------------------------------------------------------------------------- |
| Распознавание речи из аудиофайлов формата WAV               | ✅ Выполнено | Полностью реализовано и протестировано (AP-01, SRS-03)                            |
| Потоковое распознавание фрагментов речи                     | ✅ Выполнено | Реализована обработка потоков аудиоданных (SRS-02, IT-02)                         |
| Обработка как целых файлов, так и отдельных аудиофрагментов | ✅ Выполнено | Поддерживаются оба режима работы (SRS-03, SRS-04)                                 |
| Оповещение о результатах распознавания через события        | ✅ Выполнено | Реализована система событий для оповещения о процессе и результатах распознавания |
| Поддержка модели Whisper                                    | ✅ Выполнено | Интегрирована модель Whisper для локального распознавания                         |
| Возможность добавления других моделей                       | ✅ Выполнено | Разработана гибкая архитектура для добавления новых моделей (ME-01 - ME-08)       |
| Режимы обработки аудио                                      | ✅ Выполнено | Реализованы все требуемые режимы обработки                                        |
| Консольный режим для тестирования                           | ✅ Выполнено | Разработано консольное приложение для тестирования                                |

### Ключевые требования

| Требование                                      | Статус       | Комментарий                                                                     |
| ----------------------------------------------- | ------------ | ------------------------------------------------------------------------------- |
| Корректная работа с форматом WAV                | ✅ Выполнено | Реализована и протестирована обработка WAV-файлов (AP-01, AP-02)                |
| Возможность асинхронной обработки фрагментов    | ✅ Выполнено | Реализована полноценная асинхронная обработка (SRS-09, SRS-10)                  |
| Управление очередью фрагментов на распознавание | ✅ Выполнено | Разработаны механизмы управления очередью с приоритетами (SM-06, SM-07, SRS-10) |
| Обработка ошибок и логирование                  | ✅ Выполнено | Реализованы механизмы восстановления после ошибок и логирование (RV-01 - RV-07) |

### Компоненты системы

| Компонент                              | Статус       | Комментарий                                                   |
| -------------------------------------- | ------------ | ------------------------------------------------------------- |
| Процессор аудио (AudioProcessor)       | ✅ Выполнено | Полностью реализован и протестирован (AP-01 - AP-05)          |
| Распознаватель речи (SpeechRecognizer) | ✅ Выполнено | Реализован с интеграцией модели Whisper (RC-01 - RC-07)       |
| Менеджер сессий (SessionManager)       | ✅ Выполнено | Реализовано управление сессиями распознавания (SM-01 - SM-11) |
| Интерфейсы взаимодействия              | ✅ Выполнено | Разработаны консольное приложение и библиотека для интеграции |

### Технический стек

| Требование                                   | Статус       | Комментарий                                                   |
| -------------------------------------------- | ------------ | ------------------------------------------------------------- |
| C# 10 или выше                               | ✅ Выполнено | Проект реализован на C# 10+                                   |
| .NET 7.0 или выше                            | ✅ Выполнено | Используется .NET 7.0 (с поддержкой .NET 9.0 согласно тестам) |
| Whisper.net для интеграции с моделью Whisper | ✅ Выполнено | Выполнена интеграция с Whisper.net                            |
| Библиотеки для работы с аудио                | ✅ Выполнено | Используются необходимые библиотеки для обработки аудио       |

### Тестирование

| Требование                                     | Статус       | Комментарий                                                            |
| ---------------------------------------------- | ------------ | ---------------------------------------------------------------------- |
| Проверка на различных аудиофайлах              | ✅ Выполнено | Тесты проведены с различными аудиофайлами (см. TestResults.txt)        |
| Тестирование с фрагментами разной длительности | ✅ Выполнено | Протестированы фрагменты разной длины (AP-04, IT-03)                   |
| Тестирование обработки ошибок                  | ✅ Выполнено | Проведены специальные тесты на обработку ошибок (IT-04, RV-01 - RV-07) |

### Общая оценка соответствия

Система полностью соответствует требованиям технического задания. Все основные функциональные и нефункциональные требования реализованы и протестированы. Разработанная архитектура обеспечивает гибкость, расширяемость и устойчивость к ошибкам, что позволяет использовать систему для распознавания речи в различных сценариях.
