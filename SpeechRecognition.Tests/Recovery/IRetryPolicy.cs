using System;
using System.Threading;
using System.Threading.Tasks;

namespace SpeechRecognition.Tests.Recovery
{
    /// <summary>
    /// Интерфейс политики повторных попыток
    /// </summary>
    public interface IRetryPolicy
    {
        /// <summary>
        /// Событие, возникающее перед попыткой повтора
        /// </summary>
        event EventHandler<RetryEventArgs> RetryAttempted;

        /// <summary>
        /// Событие, возникающее после завершения попытки повтора
        /// </summary>
        event EventHandler<RetryEventArgs> RetryCompleted;

        /// <summary>
        /// Выполняет операцию с применением политики повторных попыток
        /// </summary>
        /// <typeparam name="T">Тип результата операции</typeparam>
        /// <param name="operation">Делегат операции, которую нужно выполнить</param>
        /// <param name="operationName">Название операции (для логирования)</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Результат операции</returns>
        Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, string operationName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Выполняет операцию с применением политики повторных попыток
        /// </summary>
        /// <param name="operation">Делегат операции, которую нужно выполнить</param>
        /// <param name="operationName">Название операции (для логирования)</param>
        /// <param name="cancellationToken">Токен отмены</param>
        /// <returns>Task, представляющий асинхронную операцию</returns>
        Task ExecuteAsync(Func<CancellationToken, Task> operation, string operationName, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Аргументы события повторной попытки
    /// </summary>
    public class RetryEventArgs : EventArgs
    {
        /// <summary>
        /// Номер попытки (начиная с 1)
        /// </summary>
        public int Attempt { get; }

        /// <summary>
        /// Исключение, вызвавшее повторную попытку (null для успешных попыток)
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Название операции
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// Время до следующей попытки (TimeSpan.Zero для последней попытки)
        /// </summary>
        public TimeSpan DelayBeforeNextAttempt { get; }

        /// <summary>
        /// Создает новый экземпляр класса RetryEventArgs
        /// </summary>
        public RetryEventArgs(int attempt, Exception? exception, string operationName, TimeSpan delayBeforeNextAttempt)
        {
            Attempt = attempt;
            Exception = exception;
            OperationName = operationName;
            DelayBeforeNextAttempt = delayBeforeNextAttempt;
        }
    }
} 