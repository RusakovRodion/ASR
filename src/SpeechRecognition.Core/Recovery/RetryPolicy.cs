using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Core.Recovery
{
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
        public double BackoffMultiplier { get; set; } = 2.0;

        /// <summary>
        /// Типы исключений, которые следует обрабатывать
        /// </summary>
        public Type[] RetryableExceptions { get; set; } = new Type[] 
        { 
            typeof(TimeoutException),
            typeof(InvalidOperationException),
            typeof(Exception)
        };
    }

    /// <summary>
    /// Политика повторных попыток для восстановления после сбоев
    /// </summary>
    public class RetryPolicy
    {
        private readonly ILogger _logger;
        private readonly RetryPolicyOptions _options;

        /// <summary>
        /// Создает новый экземпляр политики повторных попыток
        /// </summary>
        /// <param name="logger">Логгер</param>
        /// <param name="options">Параметры политики (null для использования значений по умолчанию)</param>
        public RetryPolicy(ILogger logger, RetryPolicyOptions options = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? new RetryPolicyOptions();
        }

        /// <summary>
        /// Выполняет операцию с применением политики повторных попыток
        /// </summary>
        /// <typeparam name="T">Тип результата операции</typeparam>
        /// <param name="operation">Асинхронная операция</param>
        /// <param name="operationName">Имя операции (для логирования)</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Результат операции</returns>
        public async Task<T> ExecuteAsync<T>(
            Func<CancellationToken, Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            int attemptCount = 0;
            Exception lastException = null;

            while (attemptCount <= _options.MaxRetryCount)
            {
                try
                {
                    // Проверяем отмену операции
                    cancellationToken.ThrowIfCancellationRequested();

                    if (attemptCount > 0)
                    {
                        _logger.LogInformation($"Попытка {attemptCount} выполнения операции {operationName}");
                    }

                    // Выполняем операцию
                    return await operation(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Операция отменена, просто пробрасываем исключение
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    attemptCount++;

                    // Проверяем, можно ли повторить после этого исключения
                    bool canRetry = false;
                    foreach (var exceptionType in _options.RetryableExceptions)
                    {
                        if (exceptionType.IsInstanceOfType(ex))
                        {
                            canRetry = true;
                            break;
                        }
                    }

                    if (!canRetry || attemptCount > _options.MaxRetryCount)
                    {
                        _logger.LogError(ex, $"Операция {operationName} не может быть выполнена после {attemptCount} попыток");
                        throw;
                    }

                    // Рассчитываем задержку с экспоненциальным увеличением
                    int delayMs = (int)(_options.RetryDelayMs * Math.Pow(_options.BackoffMultiplier, attemptCount - 1));
                    _logger.LogWarning(ex, $"Ошибка при выполнении операции {operationName}. Повторная попытка через {delayMs} мс...");

                    try
                    {
                        await Task.Delay(delayMs, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // Задержка отменена, выходим из цикла
                        throw;
                    }
                }
            }

            // Этот код никогда не должен выполняться, т.к. в цикле мы либо возвращаем результат, либо выбрасываем исключение
            throw new InvalidOperationException($"Операция {operationName} не может быть выполнена", lastException);
        }

        /// <summary>
        /// Выполняет операцию без возвращаемого значения с применением политики повторных попыток
        /// </summary>
        /// <param name="operation">Асинхронная операция</param>
        /// <param name="operationName">Имя операции (для логирования)</param>
        /// <param name="cancellationToken">Токен отмены</param>
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> operation,
            string operationName,
            CancellationToken cancellationToken = default)
        {
            await ExecuteAsync<object>(
                async (token) => 
                {
                    await operation(token);
                    return null;
                }, 
                operationName, 
                cancellationToken);
        }
    }
} 