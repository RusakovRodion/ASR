# Архитектура проекта SpeechRecognition

Данный документ содержит подробное описание архитектуры программного комплекса для автоматического распознавания речи.

## Содержание

- [Общая архитектура](#общая-архитектура)
- [Диаграмма компонентов](#диаграмма-компонентов)
- [Компоненты системы](#компоненты-системы)
  - [Обработка аудио](#обработка-аудио)
  - [Распознавание речи](#распознавание-речи)
  - [Модели распознавания](#модели-распознавания)
  - [Управление сессиями](#управление-сессиями)
  - [Механизмы восстановления](#механизмы-восстановления)
  - [Система событий](#система-событий)
- [Поток данных и последовательность вызовов](#поток-данных-и-последовательность-вызовов)
  - [Обработка аудиофайла](#обработка-аудиофайла)
  - [Обработка потоковых данных](#обработка-потоковых-данных)
  - [Работа с очередью распознавания](#работа-с-очередью-распознавания)
- [Расширяемость системы](#расширяемость-системы)
  - [Добавление новых моделей](#добавление-новых-моделей)
  - [Настройка политик восстановления](#настройка-политик-восстановления)

## Общая архитектура

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

## Диаграмма компонентов

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

## Компоненты системы

### Обработка аудио

**Основной интерфейс**: `IAudioProcessor`

**Основная реализация**: `AudioProcessor`

**Ответственность**:
- Подготовка аудиоданных для распознавания
- Добавление WAV-заголовков к PCM-данным
- Ресемплирование и преобразование форматов
- Валидация аудиоданных

**Ключевые методы**:
- `PrepareAudioDataAsync`: Подготавливает аудиоданные для распознавания
- `PrepareAudioFileAsync`: Подготавливает аудиофайл для распознавания
- `AddWavHeader`: Добавляет WAV-заголовок к PCM-данным
- `ConvertToWav`: Конвертирует различные форматы в WAV

### Распознавание речи

**Основной интерфейс**: `ISpeechRecognizer`

**Базовый класс**: `BaseSpeechRecognizer`

**Реализации**:
- `WhisperRecognizer`: Использует Whisper модель
- `MockSpeechRecognizer`: Имитирует распознавание для тестирования
- `CustomSpeechRecognizer`: Для пользовательских моделей

**Ответственность**:
- Инициализация модели распознавания
- Распознавание речи из аудиоданных
- Освобождение ресурсов

**Ключевые методы**:
- `InitializeAsync`: Инициализирует распознаватель
- `RecognizeSpeechAsync`: Распознает речь из аудиоданных
- `Dispose`: Освобождает ресурсы

### Модели распознавания

**Интерфейсы**:
- `IModelSettings`: Настройки модели
- `IModelProvider`: Провайдер модели

**Реализации настроек**:
- `WhisperModelSettings`: Настройки для Whisper
- `MockModelSettings`: Настройки для тестирования
- `CustomModelSettings`: Пользовательские настройки

**Реализации провайдеров**:
- `WhisperModelProvider`: Провайдер для Whisper
- `MockModelProvider`: Провайдер для тестирования
- `CustomModelProvider`: Пользовательский провайдер

**Ответственность**:
- Загрузка моделей
- Обеспечение доступности моделей
- Настройка параметров моделей

**Ключевые методы**:
- `EnsureModelExistsAsync`: Обеспечивает наличие модели
- `IsCompatible`: Проверяет совместимость настроек

### Управление сессиями

**Основные классы**:
- `SessionManager`: Управляет сессиями распознавания
- `RecognitionSession`: Представляет сессию распознавания
- `RecognitionResult`: Результат распознавания

**Ответственность**:
- Создание и управление сессиями
- Управление очередью распознавания
- Координация работы распознавателей
- Отслеживание статусов и результатов

**Ключевые методы**:
- `CreateSessionAsync`: Создает новую сессию
- `EnqueueRecognitionItemAsync`: Добавляет элемент в очередь
- `ProcessQueueAsync`: Обрабатывает очередь
- `GetSessionStatus`: Получает статус сессии

### Механизмы восстановления

**Основные классы**:
- `RetryPolicy`: Политика повторных попыток
- `RetryPolicyFactory`: Создание политик

**Типы политик**:
- Линейная задержка
- Экспоненциальная задержка
- Задержка с джиттером

**Ответственность**:
- Обработка временных сбоев
- Повторные попытки операций
- Логирование ошибок

**Ключевые методы**:
- `ExecuteAsync`: Выполняет операцию с повторными попытками
- `CreateExponentialBackoff`: Создает политику с экспоненциальной задержкой
- `CreateConstantBackoff`: Создает политику с постоянной задержкой

### Система событий

**Основные классы**:
- `RecognitionEventArgs`: События распознавания
- `RecognitionErrorEventArgs`: События ошибок
- `SessionEventArgs`: События сессий
- `FragmentStatusChangedEventArgs`: События статуса фрагментов
- `QueueStateChangedEventArgs`: События состояния очереди

**Ответственность**:
- Оповещение о ходе распознавания
- Передача результатов распознавания
- Уведомление об ошибках
- Отслеживание статусов

## Поток данных и последовательность вызовов

### Обработка аудиофайла

1. Клиент вызывает `ISpeechRecognitionService.ProcessFileAsync`
2. Сервис использует `AudioProcessor` для чтения и подготовки файла
3. Создается сессия через `SessionManager`
4. Аудиоданные передаются в `ISpeechRecognizer`
5. Распознаватель использует модель для распознавания
6. Результат возвращается клиенту
7. Генерируется событие `RecognitionCompleted`

### Обработка потоковых данных

1. Клиент вызывает `ISpeechRecognitionService.ProcessStreamAsync`
2. Аудиоданные проверяются и подготавливаются с помощью `AudioProcessor`
3. Сервис передает данные в распознаватель
4. Распознавание выполняется асинхронно
5. Результат возвращается клиенту
6. Генерируется событие `RecognitionCompleted`

### Работа с очередью распознавания

1. Клиент добавляет фрагменты через `EnqueueRecognitionItemAsync`
2. Каждый фрагмент получает уникальный идентификатор и TaskCompletionSource
3. Клиент запускает обработку очереди через `ProcessQueueAsync`
4. `SessionManager` обрабатывает элементы в порядке приоритета
5. Статус фрагментов изменяется (Pending → Processing → Completed/Failed)
6. Генерируются события `QueueItemStatusChanged`
7. По завершении обработки результаты доступны через результаты соответствующих задач

## Расширяемость системы

### Добавление новых моделей

Для добавления новой модели необходимо:

1. Создать класс настроек, реализующий `IModelSettings`:
   ```csharp
   public class NewModelSettings : IModelSettings
   {
       public RecognizerType RecognizerType => RecognizerType.Custom;
       public string ModelPath { get; }
       public string Language { get; }
       
       public NewModelSettings(string modelPath, string language = "ru")
       {
           ModelPath = modelPath;
           Language = language;
       }
   }
   ```

2. Создать провайдер модели, реализующий `IModelProvider`:
   ```csharp
   public class NewModelProvider : IModelProvider
   {
       private readonly ILogger _logger;
       
       public NewModelProvider(ILogger logger)
       {
           _logger = logger;
       }
       
       public async Task<string> EnsureModelExistsAsync(IModelSettings modelSettings)
       {
           // Реализация загрузки модели
       }
       
       public bool IsCompatible(IModelSettings modelSettings)
       {
           return modelSettings is NewModelSettings;
       }
   }
   ```

3. Создать распознаватель, наследующийся от `BaseSpeechRecognizer`:
   ```csharp
   public class NewModelRecognizer : BaseSpeechRecognizer
   {
       public override RecognizerType RecognizerType => RecognizerType.Custom;
       
       public NewModelRecognizer(IAudioProcessor audioProcessor, IModelSettings settings, ILogger logger)
           : base(audioProcessor, settings) { }
       
       public override async Task InitializeAsync()
       {
           // Инициализация
       }
       
       public override async Task<string> RecognizeSpeechAsync(byte[] audioData, CancellationToken cancellationToken = default)
       {
           // Реализация распознавания
       }
   }
   ```

4. Зарегистрировать компоненты в фабриках:
   ```csharp
   // В ModelProviderFactory
   public static IModelProvider CreateProvider(RecognizerType type, ILogger logger)
   {
       return type switch
       {
           // ...
           RecognizerType.Custom => new NewModelProvider(logger),
           // ...
       };
   }
   
   // В SpeechRecognizerFactory
   public static ISpeechRecognizer CreateRecognizer(...)
   {
       return settings.RecognizerType switch
       {
           // ...
           RecognizerType.Custom when settings is NewModelSettings => 
               new NewModelRecognizer(audioProcessor, settings, logger),
           // ...
       };
   }
   ```

### Настройка политик восстановления

Система поддерживает различные политики повторных попыток:

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