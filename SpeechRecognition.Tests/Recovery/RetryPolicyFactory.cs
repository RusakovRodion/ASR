using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Tests.Recovery
{
    /// <summary>
    /// Фабрика для создания политик повторных попыток
    /// </summary>
    public static class RetryPolicyFactory
    {
        /// <summary>
        /// Создает политику повторных попыток с параметрами по умолчанию
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика повторных попыток</returns>
        public static IRetryPolicy CreateDefault(ILogger logger)
        {
            return new DefaultRetryPolicy(logger);
        }

        /// <summary>
        /// Создает политику повторных попыток с указанными параметрами
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="options">Параметры политики повторных попыток</param>
        /// <returns>Политика повторных попыток</returns>
        public static IRetryPolicy Create(ILogger logger, RetryPolicyOptions options)
        {
            return new DefaultRetryPolicy(logger, options);
        }

        /// <summary>
        /// Создает политику повторных попыток, оптимизированную для распознавания речи
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика повторных попыток</returns>
        public static IRetryPolicy CreateForSpeechRecognition(ILogger logger)
        {
            return new DefaultRetryPolicy(logger, new RetryPolicyOptions
            {
                MaxRetryCount = 3,
                RetryDelayMs = 500,
                BackoffMultiplier = 2.0,
                RetryableExceptions = new[] { typeof(InvalidOperationException), typeof(TimeoutException) }
            });
        }

        /// <summary>
        /// Создает политику повторных попыток, оптимизированную для инициализации моделей
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <returns>Политика повторных попыток</returns>
        public static IRetryPolicy CreateForModelInitialization(ILogger logger)
        {
            return new DefaultRetryPolicy(logger, new RetryPolicyOptions
            {
                MaxRetryCount = 5,
                RetryDelayMs = 1000,
                BackoffMultiplier = 1.5,
                RetryableExceptions = new[] { typeof(InvalidOperationException), typeof(TimeoutException) }
            });
        }
    }

    /// <summary>
    /// Параметры политики повторных попыток
    /// </summary>
    public class RetryPolicyOptions
    {
        /// <summary>
        /// Максимальное количество повторных попыток
        /// </summary>
        public int MaxRetryCount { get; set; } = 3;

        /// <summary>
        /// Базовая задержка перед повтором (в миллисекундах)
        /// </summary>
        public int RetryDelayMs { get; set; } = 500;

        /// <summary>
        /// Множитель для экспоненциального увеличения задержки
        /// </summary>
        public double BackoffMultiplier { get; set; } = 1.5;

        /// <summary>
        /// Типы исключений, которые следует обрабатывать
        /// </summary>
        public Type[] RetryableExceptions { get; set; } = { typeof(Exception) };
    }

    /// <summary>
    /// Реализация политики повторных попыток по умолчанию для тестов
    /// </summary>
    internal class DefaultRetryPolicy : IRetryPolicy
    {
        private readonly ILogger _logger;
        private readonly RetryPolicyOptions _options;

        public event EventHandler<RetryEventArgs>? RetryAttempted;
        public event EventHandler<RetryEventArgs>? RetryCompleted;

        public DefaultRetryPolicy(ILogger logger, RetryPolicyOptions? options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new RetryPolicyOptions();
        }

        public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, string operationName, CancellationToken cancellationToken = default)
        {
            int attempt = 0;
            Exception? lastException = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (attempt > 0)
                    {
                        // Рассчитываем задержку
                        int delayMs = (int)(_options.RetryDelayMs * Math.Pow(_options.BackoffMultiplier, attempt - 1));
                        TimeSpan delay = TimeSpan.FromMilliseconds(delayMs);

                        // Уведомляем о попытке повтора
                        RetryEventArgs args = new RetryEventArgs(attempt, lastException, operationName, delay);
                        RetryAttempted?.Invoke(this, args);

                        // Задержка перед повторной попыткой
                        await Task.Delay(delayMs, cancellationToken);
                    }

                    // Выполняем операцию
                    T result = await operation(cancellationToken);

                    // Для повторных попыток уведомляем об успешном выполнении
                    if (attempt > 0)
                    {
                        RetryEventArgs args = new RetryEventArgs(attempt, null, operationName, TimeSpan.Zero);
                        RetryCompleted?.Invoke(this, args);
                    }

                    return result;
                }
                catch (OperationCanceledException)
                {
                    // Не перехватываем исключения отмены операции
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    // Проверяем, можно ли повторить попытку
                    bool canRetry = false;
                    foreach (var type in _options.RetryableExceptions)
                    {
                        if (type.IsInstanceOfType(ex))
                        {
                            canRetry = true;
                            break;
                        }
                    }

                    // Увеличиваем счетчик попыток
                    attempt++;

                    // Проверяем, превышено ли максимальное количество попыток
                    if (!canRetry || attempt > _options.MaxRetryCount)
                    {
                        // Уведомляем о завершении попыток с ошибкой
                        RetryEventArgs args = new RetryEventArgs(attempt, ex, operationName, TimeSpan.Zero);
                        RetryCompleted?.Invoke(this, args);

                        throw new InvalidOperationException($"Исчерпаны все попытки выполнения операции '{operationName}' ({attempt} из {_options.MaxRetryCount})", ex);
                    }

                    // Логируем ошибку и продолжаем
                    _logger.LogWarning(ex, $"Ошибка при выполнении операции '{operationName}'. Повторная попытка {attempt} из {_options.MaxRetryCount}...");
                }
            }
        }

        public async Task ExecuteAsync(Func<CancellationToken, Task> operation, string operationName, CancellationToken cancellationToken = default)
        {
            await ExecuteAsync<object>(async (ct) =>
            {
                await operation(ct);
                return null!;
            }, operationName, cancellationToken);
        }
    }
} 