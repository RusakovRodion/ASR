using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SpeechRecognition.Core.Events;
using SpeechRecognition.Core.Sessions;

namespace SpeechRecognition.Core
{
    /// <summary>
    /// Интерфейс сервиса распознавания речи для внешних приложений
    /// </summary>
    public interface ISpeechRecognitionService : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Событие, возникающее при получении результата распознавания речи
        /// </summary>
        event EventHandler<RecognitionEventArgs> RecognitionCompleted;

        /// <summary>
        /// Событие, возникающее перед началом распознавания речи
        /// </summary>
        event EventHandler<RecognitionEventArgs> RecognitionStarted;

        /// <summary>
        /// Событие изменения статуса элемента очереди
        /// </summary>
        event EventHandler<FragmentStatusChangedEventArgs> QueueItemStatusChanged;
        
        /// <summary>
        /// Событие изменения состояния очереди (активна/приостановлена)
        /// </summary>
        event EventHandler<QueueStateChangedEventArgs> QueueStateChanged;

        /// <summary>
        /// Инициализирует сервис распознавания речи
        /// </summary>
        /// <returns>Task, представляющий асинхронную операцию инициализации</returns>
        Task InitializeAsync();

        /// <summary>
        /// Обрабатывает фрагмент аудиоданных из потока
        /// </summary>
        /// <param name="audioChunk">Фрагмент аудиоданных (PCM или WAV)</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        Task<string> ProcessStreamAsync(byte[] audioChunk, CancellationToken cancellationToken = default);

        /// <summary>
        /// Обрабатывает аудиофайл целиком
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        Task<string> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Обрабатывает аудиофайл с разбиением на указанное количество фрагментов
        /// </summary>
        /// <param name="filePath">Путь к аудиофайлу</param>
        /// <param name="numChunks">Количество фрагментов</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Результат распознавания речи</returns>
        Task<string> ProcessFileInChunksAsync(string filePath, int numChunks, CancellationToken cancellationToken = default);

        // Методы для управления очередью распознавания

        /// <summary>
        /// Добавляет фрагмент в очередь распознавания
        /// </summary>
        /// <param name="audioChunk">Аудиоданные фрагмента</param>
        /// <param name="priority">Приоритет (меньшее значение - более высокий приоритет)</param>
        /// <param name="metadata">Пользовательские метаданные</param>
        /// <param name="processImmediately">Обработать немедленно, минуя очередь</param>
        /// <returns>Идентификатор элемента очереди и задача для отслеживания результата</returns>
        Task<(Guid QueueItemId, Task<string> ResultTask)> EnqueueRecognitionItemAsync(
            byte[] audioChunk, 
            int priority = 0, 
            string metadata = "", 
            bool processImmediately = false);

        /// <summary>
        /// Изменяет приоритет фрагмента в очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <param name="newPriority">Новый приоритет</param>
        /// <returns>true, если приоритет успешно изменен</returns>
        bool ChangeItemPriority(Guid itemId, int newPriority);

        /// <summary>
        /// Отменяет обработку фрагмента
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если обработка успешно отменена</returns>
        bool CancelQueueItem(Guid itemId);

        /// <summary>
        /// Получает информацию о фрагменте в очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>Информация о фрагменте или null, если не найден</returns>
        QueueItem GetQueueItem(Guid itemId);

        /// <summary>
        /// Получает список элементов очереди
        /// </summary>
        /// <param name="statusFilter">Фильтр по статусу (null для всех элементов)</param>
        /// <returns>Список элементов очереди</returns>
        IReadOnlyList<QueueItem> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null);

        /// <summary>
        /// Приостанавливает обработку очереди
        /// </summary>
        void PauseQueue();

        /// <summary>
        /// Возобновляет обработку очереди
        /// </summary>
        void ResumeQueue();

        /// <summary>
        /// Очищает очередь распознавания
        /// </summary>
        /// <param name="cancelProcessing">Отменять ли текущие операции распознавания</param>
        void ClearQueue(bool cancelProcessing = false);

        /// <summary>
        /// Отменяет все операции распознавания
        /// </summary>
        void CancelAllOperations();

        /// <summary>
        /// Удаляет элемент из очереди
        /// </summary>
        /// <param name="itemId">Идентификатор элемента очереди</param>
        /// <returns>true, если элемент успешно удален</returns>
        bool RemoveQueueItem(Guid itemId);

        /// <summary>
        /// Возвращает количество элементов в очереди с указанным статусом
        /// </summary>
        /// <param name="status">Статус (null для всех элементов)</param>
        /// <returns>Количество элементов</returns>
        int GetQueueItemCount(RecognitionQueueItemStatus? status = null);
        
        /// <summary>
        /// Запускает обработку очереди с указанным уровнем параллелизма
        /// </summary>
        /// <param name="maxParallelProcessing">Максимальное количество одновременно обрабатываемых фрагментов</param>
        /// <param name="cancellationToken">Токен отмены операции</param>
        /// <returns>Задача, представляющая асинхронную операцию</returns>
        Task ProcessQueueAsync(int maxParallelProcessing = 1, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Статус элемента очереди распознавания
    /// </summary>
    public enum RecognitionQueueItemStatus
    {
        /// <summary>
        /// Ожидает обработки
        /// </summary>
        Pending,

        /// <summary>
        /// Находится в процессе обработки
        /// </summary>
        Processing,

        /// <summary>
        /// Обработка завершена успешно
        /// </summary>
        Completed,

        /// <summary>
        /// Обработка завершена с ошибкой
        /// </summary>
        Failed,

        /// <summary>
        /// Обработка отменена
        /// </summary>
        Canceled
    }
    
    /// <summary>
    /// Информация об элементе очереди распознавания
    /// </summary>
    public class QueueItem
    {
        /// <summary>
        /// Идентификатор элемента
        /// </summary>
        public Guid Id { get; }
        
        /// <summary>
        /// Статус элемента
        /// </summary>
        public RecognitionQueueItemStatus Status { get; }
        
        /// <summary>
        /// Приоритет обработки (меньшее значение - более высокий приоритет)
        /// </summary>
        public int Priority { get; }
        
        /// <summary>
        /// Время добавления в очередь
        /// </summary>
        public DateTime EnqueueTime { get; }
        
        /// <summary>
        /// Пользовательские метаданные
        /// </summary>
        public string Metadata { get; }
        
        /// <summary>
        /// Размер аудиоданных в байтах
        /// </summary>
        public int AudioDataSize { get; }
        
        /// <summary>
        /// Создает информацию об элементе очереди
        /// </summary>
        public QueueItem(
            Guid id, 
            RecognitionQueueItemStatus status, 
            int priority, 
            DateTime enqueueTime, 
            string metadata, 
            int audioDataSize)
        {
            Id = id;
            Status = status;
            Priority = priority;
            EnqueueTime = enqueueTime;
            Metadata = metadata;
            AudioDataSize = audioDataSize;
        }
    }
} 