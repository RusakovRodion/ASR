using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Провайдер для пользовательской модели распознавания
    /// </summary>
    public class CustomModelProvider : IModelProvider
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Custom;

        /// <summary>
        /// Создает новый экземпляр провайдера пользовательской модели
        /// </summary>
        /// <param name="logger">Логгер</param>
        public CustomModelProvider(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Проверяет наличие модели и загружает её при необходимости
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>Путь к загруженной модели</returns>
        public Task<string> EnsureModelExistsAsync(IModelSettings modelSettings)
        {
            if (!IsCompatible(modelSettings))
            {
                throw new ArgumentException("Несовместимые настройки модели", nameof(modelSettings));
            }

            var customSettings = (CustomModelSettings)modelSettings;
            
            // Проверяем существование файла модели
            if (File.Exists(customSettings.ModelPath))
            {
                _logger.LogInformation($"Пользовательская модель найдена: {customSettings.ModelPath}");
                return Task.FromResult(customSettings.ModelPath);
            }

            // Здесь можно добавить код для автоматической загрузки модели с удаленного сервера,
            // например, с HuggingFace или другого хранилища моделей.
            // В данном примере просто возвращаем ошибку, если файл не существует.
            
            throw new FileNotFoundException(
                $"Файл пользовательской модели не найден: {customSettings.ModelPath}. " +
                "Для пользовательских моделей требуется предварительная загрузка модели вручную.",
                customSettings.ModelPath);
        }

        /// <summary>
        /// Проверяет совместимость настроек с данным провайдером
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>true, если настройки совместимы</returns>
        public bool IsCompatible(IModelSettings modelSettings)
        {
            return modelSettings is CustomModelSettings && modelSettings.RecognizerType == RecognizerType.Custom;
        }
    }
} 