using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Провайдер моделей для тестирования
    /// </summary>
    public class MockModelProvider : IModelProvider
    {
        private readonly ILogger _logger;

        /// <summary>
        /// Получает тип распознавателя
        /// </summary>
        public RecognizerType RecognizerType => RecognizerType.Mock;

        /// <summary>
        /// Создает новый экземпляр провайдера моделей для тестирования
        /// </summary>
        /// <param name="logger">Логгер</param>
        public MockModelProvider(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Проверяет наличие модели (в мок-режиме просто возвращает путь)
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>Путь к модели</returns>
        public Task<string> EnsureModelExistsAsync(IModelSettings modelSettings)
        {
            if (!IsCompatible(modelSettings))
            {
                throw new ArgumentException("Несовместимые настройки модели", nameof(modelSettings));
            }

            _logger.LogInformation("Используем мок-модель для тестирования");
            return Task.FromResult(modelSettings.ModelPath);
        }

        /// <summary>
        /// Проверяет совместимость настроек с данным провайдером
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>true, если настройки совместимы</returns>
        public bool IsCompatible(IModelSettings modelSettings)
        {
            return modelSettings is MockModelSettings && modelSettings.RecognizerType == RecognizerType.Mock;
        }
    }
} 