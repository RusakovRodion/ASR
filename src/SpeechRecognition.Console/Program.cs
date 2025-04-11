using System;
using System.CommandLine;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core;

namespace SpeechRecognition.Console
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // Настройка логгера
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            var logger = loggerFactory.CreateLogger<Program>();

            // Получение пути к тестовому файлу, если аргументы не указаны
            if (args.Length == 0)
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                // Переходим на два уровня выше (bin/Debug/net9.0 -> проект)
                string projectPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", ".."));
                // Переходим на уровень выше (проект -> решение)
                string solutionPath = Path.GetFullPath(Path.Combine(projectPath, ".."));
                // Переходим на уровень выше (решение -> корень)
                string rootPath = Path.GetFullPath(Path.Combine(solutionPath, ".."));
                
                string testAudioPath = Path.Combine(rootPath, "audioExamples", "test.wav");
                string modelPath = Path.Combine(rootPath, "models", "ggml-tiny.bin");
                
                if (File.Exists(testAudioPath))
                {
                    logger.LogInformation($"Запуск с тестовым файлом: {testAudioPath}");
                    args = new[] { "--file", testAudioPath, "--model", modelPath, "--language", "ru" };
                }
                else
                {
                    logger.LogError($"Тестовый файл не найден: {testAudioPath}");
                }
            }

            // Опция для выбора файла
            var fileOption = new Option<FileInfo>(
                new[] { "--file", "-f" },
                "Путь к аудиофайлу для распознавания");
            
            // Опция для выбора языка
            var languageOption = new Option<string>(
                new[] { "--language", "-l" },
                () => "ru",
                "Язык для распознавания (по умолчанию: ru)");

            // Опция для выбора модели
            var modelOption = new Option<FileInfo>(
                new[] { "--model", "-m" },
                () => new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "models", "ggml-tiny.bin")),
                "Путь к файлу модели Whisper");

            // Создание корневой команды
            var rootCommand = new RootCommand("Консольное приложение для распознавания речи");
            rootCommand.AddOption(fileOption);
            rootCommand.AddOption(languageOption);
            rootCommand.AddOption(modelOption);

            rootCommand.SetHandler(async (FileInfo file, string language, FileInfo model) =>
            {
                await ProcessFile(file.FullName, language, model.FullName, logger);
            }, fileOption, languageOption, modelOption);

            // Выполнение команды
            return await rootCommand.InvokeAsync(args);
        }

        private static async Task ProcessFile(string filePath, string language, string modelPath, ILogger logger, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                logger.LogError($"Файл не найден: {filePath}");
                return;
            }

            logger.LogInformation($"Начало распознавания файла: {filePath}");
            logger.LogInformation($"Модель: {modelPath}");
            logger.LogInformation($"Язык: {language}");

            try
            {
                // Определение типа модели по имени файла
                string modelType = Path.GetFileNameWithoutExtension(modelPath).Contains("tiny") ? "tiny" : "base";
                
                using var processor = new WhisperStreamProcessor(logger, modelPath, language, modelType);
                
                // Инициализация процессора (при необходимости скачает модель)
                await processor.InitializeAsync();

                // Обработка файла
                var result = await processor.ProcessFileAsync(filePath, cancellationToken);

                logger.LogInformation("Результат распознавания:");
                System.Console.WriteLine(result);
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Операция была отменена пользователем");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Произошла ошибка при распознавании речи");
            }
        }
    }
}
