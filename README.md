# Система автоматического распознавания речи (ASR)

Программный комплекс для автоматического распознавания речи, разработанный для обработки аудиофрагментов, нарезанных по тишине внешним приложением.

## Особенности

- Распознавание русской речи с использованием модели Whisper
- Поддержка как целых WAV-файлов, так и потоковых фрагментов PCM-данных
- Асинхронная обработка аудиоданных
- Управление сессиями распознавания
- Добавление WAV-заголовков к PCM-данным
- Ресемплирование аудио при необходимости

## Структура проекта

- **SpeechRecognition.Core** - основная библиотека для распознавания речи
  - **Audio** - компоненты для обработки аудиоданных
  - **Recognition** - компоненты для распознавания речи
  - **Sessions** - компоненты для управления сессиями распознавания
  - **Utils** - вспомогательные утилиты
- **SpeechRecognition.Console** - консольное приложение для демонстрации работы

## Требования

- .NET 7.0 или выше
- Модель Whisper (ggml-base.bin или аналогичная)

## Установка

1. Клонировать репозиторий:
```bash
git clone https://github.com/username/speech-recognition.git
cd speech-recognition
```

2. Построить проект:
```bash
dotnet build
```

3. Скачать модель Whisper:
```bash
mkdir models
# Скачайте модель Whisper в формате GGML из официального репозитория
# и поместите ее в директорию models
```

## Использование

### Консольное приложение

```bash
cd src/SpeechRecognition.Console/bin/Debug/net7.0
dotnet SpeechRecognition.Console.dll --file path/to/audio.wav --language ru --model path/to/model.bin
```

Параметры:
- `--file`, `-f` - путь к аудиофайлу для распознавания
- `--language`, `-l` - язык для распознавания (по умолчанию: ru)
- `--model`, `-m` - путь к файлу модели Whisper (по умолчанию: models/ggml-base.bin)

### Использование как библиотеки

```csharp
// Создание процессора с указанием пути к модели и языка
using var logger = loggerFactory.CreateLogger<MyClass>();
using var processor = new WhisperStreamProcessor(logger, "path/to/model.bin", "ru");

// Инициализация процессора
await processor.InitializeAsync();

// Обработка файла
string result = await processor.ProcessFileAsync("path/to/audio.wav");
Console.WriteLine(result);

// Обработка потока аудиофрагментов
byte[] audioChunk = GetAudioChunk(); // Получение аудиофрагмента
string recognizedText = await processor.ProcessStreamAsync(audioChunk);
Console.WriteLine(recognizedText);
```

## Лицензия

MIT 