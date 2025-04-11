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

- .NET 9.0 или выше
- Модель Whisper (ggml-tiny.bin или аналогичная)

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
# Использование опубликованной версии
cd publish
./whisper-stream --file path/to/audio.wav --language ru --model path/to/model.bin --num-chunks 1
```

или

```bash
# Использование версии для разработки
cd src
dotnet run --project SpeechRecognition.Console -- --file path/to/audio.wav --language ru --num-chunks 2
```

Параметры:
- `--file`, `-f` - путь к аудиофайлу для распознавания
- `--language`, `-l` - язык для распознавания (по умолчанию: ru)
- `--model`, `-m` - путь к файлу модели Whisper (по умолчанию: models/ggml-tiny.bin)
- `--num-chunks`, `-n` - количество фрагментов для обработки аудиофайла (по умолчанию: 1)

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

// Обработка файла с разбиением на фрагменты
int numChunks = 3; // Количество фрагментов
string resultChunks = await processor.ProcessFileInChunksAsync("path/to/audio.wav", numChunks);
Console.WriteLine(resultChunks);

// Обработка потока аудиофрагментов
byte[] audioChunk = GetAudioChunk(); // Получение аудиофрагмента
string recognizedText = await processor.ProcessStreamAsync(audioChunk);
Console.WriteLine(recognizedText);
```

## Лицензия

MIT 