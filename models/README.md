# Модели Whisper для распознавания речи

В этой директории должны быть размещены модели Whisper в формате GGML для распознавания речи.

## Как скачать модель

1. Скачайте предварительно сконвертированную модель из репозитория Whisper.cpp или Whisper.net
   - Репозиторий [Whisper.cpp](https://github.com/ggerganov/whisper.cpp) 
   - Репозиторий [Whisper.net](https://github.com/sandrohanea/whisper.net)

2. Можно использовать следующие модели:
   - `ggml-tiny.bin` - самая маленькая модель (~ 75 МБ)
   - `ggml-base.bin` - базовая модель (~ 142 МБ)
   - `ggml-small.bin` - малая модель (~ 466 МБ)
   - `ggml-medium.bin` - средняя модель (~ 1.5 ГБ)

3. Поместите скачанный файл модели в эту директорию.

## Использование модели

При запуске консольного приложения укажите путь к файлу модели с помощью параметра `--model`:

```bash
dotnet run --project src/SpeechRecognition.Console/SpeechRecognition.Console.csproj --file path/to/audio.wav --model models/ggml-base.bin
```

Если параметр `--model` не указан, по умолчанию будет использоваться `models/ggml-base.bin`. 