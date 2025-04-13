using System;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Recovery
{
    /// <summary>
    /// Фабрика для создания политик восстановления
    /// </summary>
    public static class RetryPolicyFactory
    {
        /// <summary>
        /// Создает политику восстановления по умолчанию
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика восстановления</returns>
        public static RetryPolicy CreateDefault(ILogger logger)
        {
            return new RetryPolicy(logger);
        }

        /// <summary>
        /// Создает политику восстановления для распознавания речи
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика восстановления для распознавания речи</returns>
        public static RetryPolicy CreateForSpeechRecognition(ILogger logger)
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryCount = 3,
                RetryDelayMs = 1000,
                BackoffMultiplier = 1.5,
                RetryableExceptions = new Type[]
                {
                    typeof(TimeoutException),
                    typeof(InvalidOperationException),
                    typeof(Exception)
                }
            };

            return new RetryPolicy(logger, options);
        }

        /// <summary>
        /// Создает политику восстановления для инициализации моделей
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика восстановления для инициализации моделей</returns>
        public static RetryPolicy CreateForModelInitialization(ILogger logger)
        {
            var options = new RetryPolicyOptions
            {
                MaxRetryCount = 5,
                RetryDelayMs = 2000,
                BackoffMultiplier = 2.0,
                RetryableExceptions = new Type[]
                {
                    typeof(TimeoutException),
                    typeof(InvalidOperationException),
                    typeof(System.IO.IOException),
                    typeof(Exception)
                }
            };

            return new RetryPolicy(logger, options);
        }

        /// <summary>
        /// Создает политику восстановления с пользовательскими параметрами
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="options">Пользовательские параметры</param>
        /// <returns>Политика восстановления</returns>
        public static RetryPolicy Create(ILogger logger, RetryPolicyOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            return new RetryPolicy(logger, options);
        }
    }
} 