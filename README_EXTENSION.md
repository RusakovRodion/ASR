# Руководство по расширению системы

Данный документ описывает способы добавления новых моделей распознавания речи в систему.

## Содержание

- [Архитектура системы расширений](#архитектура-системы-расширений)
- [Добавление новой модели распознавания](#добавление-новой-модели-распознавания)
  - [1. Создание настроек модели](#1-создание-настроек-модели)
  - [2. Создание провайдера модели](#2-создание-провайдера-модели)
  - [3. Создание распознавателя](#3-создание-распознавателя)
  - [4. Регистрация в фабриках](#4-регистрация-в-фабриках)
- [Пример использования пользовательской модели](#пример-использования-пользовательской-модели)
- [Тестирование расширений](#тестирование-расширений)

## Архитектура системы расширений

Система поддерживает расширение для работы с различными моделями распознавания речи через систему абстракций:

1. **Настройки модели** - классы, реализующие интерфейс `IModelSettings`, содержат параметры конкретной модели.
2. **Провайдеры моделей** - классы, реализующие интерфейс `IModelProvider`, отвечают за загрузку модели.
3. **Распознаватели** - классы, наследующие от `BaseSpeechRecognizer`, выполняют собственно распознавание.

Взаимодействие между компонентами происходит через фабрики, которые создают нужные экземпляры на основе типа модели.

## Добавление новой модели распознавания

### 1. Создание настроек модели

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

### 2. Создание провайдера модели

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

### 3. Создание распознавателя

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

### 4. Регистрация в фабриках

#### Добавление типа распознавателя

Если вам требуется новый тип распознавателя (не Custom), обновите перечисление `RecognizerType`:

```csharp
public enum RecognizerType
{
    Whisper,
    Mock,
    Custom,
    MyModel // Ваш новый тип
}
```

#### Обновление фабрик

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

#### Создание метода в SpeechRecognitionServiceFactory

```csharp
public static ISpeechRecognitionService CreateMyModelService(
    ILogger logger,
    string modelPath,
    string language = "ru",
    bool useGPU = false,
    int beamSize = 5)
{
    if (logger == null)
        throw new ArgumentNullException(nameof(logger));

    if (string.IsNullOrEmpty(modelPath))
        throw new ArgumentException("Путь к модели не может быть пустым", nameof(modelPath));

    var modelSettings = new MyModelSettings(modelPath, language, useGPU, beamSize);
    return CreateService(logger, modelSettings);
}
```

## Пример использования пользовательской модели

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

## Тестирование расширений

Для тестирования новых моделей рекомендуется:

1. Создать юнит-тесты для проверки работы провайдера и распознавателя
2. Использовать мок-объекты для внешних зависимостей
3. Проверить работу с реальными аудиофайлами

Пример тестового класса:

```csharp
public class MyModelTests
{
    [Fact]
    public async Task MyModel_RecognizesAudio_Successfully()
    {
        // Arrange
        var logger = new Mock<ILogger>();
        var audioProcessor = new AudioProcessor();
        var settings = new MyModelSettings("test_model.bin");
        
        var recognizer = new MyModelRecognizer(audioProcessor, settings, logger.Object);
        
        // Act
        await recognizer.InitializeAsync();
        string result = await recognizer.RecognizeSpeechAsync(GetTestAudio());
        
        // Assert
        Assert.NotEmpty(result);
    }
}
``` 