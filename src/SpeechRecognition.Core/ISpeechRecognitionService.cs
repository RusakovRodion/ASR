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
    public interface ISpeechRecognitionService : IDisposable
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
        /// <returns>Идентификатор элемента очереди</returns>
        Guid EnqueueRecognitionItem(byte[] audioChunk, int priority = 0, string metadata = "");

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
        object GetQueueItem(Guid itemId);

        /// <summary>
        /// Получает список элементов очереди
        /// </summary>
        /// <param name="statusFilter">Фильтр по статусу (null для всех элементов)</param>
        /// <returns>Список элементов очереди</returns>
        IReadOnlyList<object> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null);

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
    }
} 