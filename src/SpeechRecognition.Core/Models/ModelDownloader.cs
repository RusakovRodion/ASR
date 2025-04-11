using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Класс для загрузки моделей Whisper
    /// </summary>
    public class ModelDownloader
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        // URL для скачивания моделей
        private const string WHISPER_TINY_URL = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin";
        private const string WHISPER_BASE_URL = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin";

        /// <summary>
        /// Создает новый экземпляр класса ModelDownloader
        /// </summary>
        /// <param name="logger">Логгер</param>
        public ModelDownloader(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Проверяет наличие модели и скачивает её при необходимости
        /// </summary>
        /// <param name="modelPath">Путь к модели</param>
        /// <param name="modelType">Тип модели (tiny, base)</param>
        /// <returns>Полный путь к модели</returns>
        public async Task<string> EnsureModelExistsAsync(string modelPath, string modelType = "base")
        {
            if (File.Exists(modelPath))
            {
                _logger.LogInformation($"Модель найдена по пути: {modelPath}");
                return modelPath;
            }

            // Создаем директорию, если она не существует
            string modelDirectory = Path.GetDirectoryName(modelPath);
            if (!string.IsNullOrEmpty(modelDirectory) && !Directory.Exists(modelDirectory))
            {
                _logger.LogInformation($"Создание директории для моделей: {modelDirectory}");
                Directory.CreateDirectory(modelDirectory);
            }

            // Определяем URL для скачивания в зависимости от типа модели
            string modelUrl = modelType.ToLower() switch
            {
                "tiny" => WHISPER_TINY_URL,
                "base" or _ => WHISPER_BASE_URL
            };

            _logger.LogInformation($"Модель не найдена. Скачивание модели {modelType} из {modelUrl}...");
            
            try
            {
                // Скачиваем модель
                var response = await _httpClient.GetAsync(modelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using (var contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(modelPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Показываем прогресс загрузки
                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    var bytesRead = 0;
                    var lastProgress = 0;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead);
                        
                        totalBytesRead += bytesRead;
                        
                        if (totalBytes > 0)
                        {
                            var progress = (int)((100.0 * totalBytesRead) / totalBytes);
                            if (progress > lastProgress + 9) // Показываем прогресс каждые 10%
                            {
                                lastProgress = progress;
                                _logger.LogInformation($"Скачивание модели: {progress}% ({totalBytesRead / (1024 * 1024)} МБ / {totalBytes / (1024 * 1024)} МБ)");
                            }
                        }
                    }
                }

                _logger.LogInformation($"Модель успешно скачана и сохранена по пути: {modelPath}");
                return modelPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Ошибка при скачивании модели: {ex.Message}");
                throw new Exception($"Не удалось скачать модель: {ex.Message}", ex);
            }
        }
    }
} 