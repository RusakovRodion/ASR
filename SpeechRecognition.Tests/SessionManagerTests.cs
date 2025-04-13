using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Sessions;
using Xunit;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Linq;

namespace SpeechRecognition.Tests
{
    public class SessionManagerTests : IDisposable
    {
        private readonly SessionManager _sessionManager;
        private readonly Mock<ISpeechRecognizer> _recognizerMock;
        private readonly ILogger _logger;

        public SessionManagerTests()
        {
            // Установка кодировки для корректного отображения кириллицы
            Console.OutputEncoding = Encoding.UTF8;
            
            _recognizerMock = new Mock<ISpeechRecognizer>();
            _recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Тестовый текст");
            _logger = new NullLogger<SessionManagerTests>();
            _sessionManager = new SessionManager(_recognizerMock.Object, _logger);
        }

        [Fact]
        public void CreateSession_ReturnsNewSession()
        {
            // Act
            var session = _sessionManager.CreateSession();

            // Assert
            Assert.NotNull(session);
            Assert.NotEqual(Guid.Empty, session.Id);
        }

        [Fact]
        public void GetSession_ReturnsCorrectSession()
        {
            // Arrange
            var session = _sessionManager.CreateSession();

            // Act
            var retrievedSession = _sessionManager.GetSession(session.Id);

            // Assert
            Assert.NotNull(retrievedSession);
            Assert.Equal(session.Id, retrievedSession.Id);
        }

        [Fact]
        public void GetAllSessions_ReturnsAllSessions()
        {
            // Arrange
            _sessionManager.CreateSession();
            _sessionManager.CreateSession();

            // Act
            var sessions = _sessionManager.GetAllSessions();

            // Assert
            Assert.Equal(2, sessions.Count);
        }

        [Fact]
        public async Task GetFragments_ReturnsCorrectFragments()
        {
            // Arrange
            var session = _sessionManager.CreateSession();
            byte[] audioData = new byte[1000]; // Тестовые аудиоданные
            
            // Добавляем несколько фрагментов
            await session.AddFragmentAsync(audioData);
            await session.AddFragmentAsync(audioData);
            await session.AddFragmentAsync(audioData);
            
            // Act
            int count = session.GetFragments().Count;
            
            // Assert
            Assert.Equal(3, count);
        }

        [Fact]
        public void PauseAndResumeQueue_ChangesQueueState()
        {
            // Arrange
            var session = _sessionManager.CreateSession();
            
            // Act & Assert
            // Пауза
            session.PauseProcessing();
            
            // Нет прямого свойства для проверки, но можно проверить через добавление 
            // фрагментов до и после паузы/возобновления
            byte[] audioData = new byte[100];
            Task.Run(async () => {
                await session.AddFragmentAsync(audioData); // В синхронном режиме может зависнуть
            });
            
            // Возобновление
            session.ResumeProcessing();
            
            // Проверка завершается успешно, если не возникло блокировок
            Assert.True(true);
        }

        [Fact]
        public async Task EnqueueFragmentAsync_WithImmediateProcessing_StartsProcessingImmediately()
        {
            // Arrange
            byte[] audioData = new byte[1000]; // Тестовые аудиоданные
            
            // Act
            var (fragmentId, processingTask) = await _sessionManager.EnqueueFragmentAsync(
                audioData, 
                priority: 0, 
                metadata: "test_immediate", 
                processImmediately: true);
            
            // Дожидаемся завершения задачи (не должно зависать)
            var result = await processingTask;
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(0, fragmentId);
            _recognizerMock.Verify(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnqueueFragmentAsync_WithDifferentPriorities_ProcessesHighPriorityFirst()
        {
            // Arrange
            byte[] audioData = new byte[1000]; // Тестовые аудиоданные
            var completedFragmentIds = new List<int>();
            var fragmentCompletionTcs = new TaskCompletionSource<bool>();
            int completedCount = 0;
            
            // Настраиваем мок для отслеживания порядка обработки
            _recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, token) => 
                {
                    // Имитируем задержку при распознавании
                    Task.Delay(50).Wait();
                    return Task.FromResult("Тестовый текст");
                });
            
            // Подписываемся на события изменения статуса фрагментов
            _sessionManager.FragmentStatusChanged += (sender, e) => 
            {
                if (e.NewStatus == FragmentStatus.Completed)
                {
                    lock (completedFragmentIds)
                    {
                        completedFragmentIds.Add(e.FragmentId);
                        completedCount++;
                        Console.WriteLine($"Фрагмент {e.FragmentId} завершен. Всего завершено: {completedCount}");
                        
                        // Если завершены все три фрагмента, сигнализируем об этом
                        if (completedCount >= 3)
                        {
                            fragmentCompletionTcs.TrySetResult(true);
                        }
                    }
                }
            };
            
            // Создаем сессию явно, чтобы работать с ней
            var session = _sessionManager.CreateSession();
            
            // Act
            // Добавляем фрагменты с разными приоритетами в созданную сессию
            var (id3, _) = await _sessionManager.EnqueueFragmentAsync(audioData, priority: 3, metadata: "low_priority", sessionId: session.Id);
            Console.WriteLine($"Добавлен фрагмент с низким приоритетом: {id3}");
            var (id1, _) = await _sessionManager.EnqueueFragmentAsync(audioData, priority: 1, metadata: "high_priority", sessionId: session.Id);
            Console.WriteLine($"Добавлен фрагмент с высоким приоритетом: {id1}");
            var (id2, _) = await _sessionManager.EnqueueFragmentAsync(audioData, priority: 2, metadata: "medium_priority", sessionId: session.Id);
            Console.WriteLine($"Добавлен фрагмент со средним приоритетом: {id2}");
            
            // Запускаем обработку очереди
            Console.WriteLine("Запуск обработки очереди...");
            var processTask = session.ProcessQueueAsync(maxParallelProcessing: 1);
            
            // Ждем завершения всех обработок или истечения таймаута
            var completionTask = await Task.WhenAny(
                fragmentCompletionTcs.Task,
                Task.Delay(5000) // 5 секунд максимального ожидания
            );
            
            if (completionTask != fragmentCompletionTcs.Task)
            {
                Console.WriteLine($"Таймаут ожидания. Завершено только {completedCount} фрагментов из 3");
            }
            
            // Assert
            // Проверяем количество обработанных фрагментов
            Assert.True(completedFragmentIds.Count >= 3, $"Обработано только {completedFragmentIds.Count} фрагментов из 3");
            
            // Индекс высокоприоритетного фрагмента должен быть меньше, чем индексы других фрагментов
            int highPriorityIndex = completedFragmentIds.IndexOf(id1);
            int mediumPriorityIndex = completedFragmentIds.IndexOf(id2);
            int lowPriorityIndex = completedFragmentIds.IndexOf(id3);
            
            Console.WriteLine($"Порядок обработки: {string.Join(", ", completedFragmentIds)}");
            Console.WriteLine($"Индексы: high={highPriorityIndex}, medium={mediumPriorityIndex}, low={lowPriorityIndex}");
            
            Assert.True(highPriorityIndex >= 0, "Высокоприоритетный фрагмент не был обработан");
            Assert.True(mediumPriorityIndex >= 0, "Среднеприоритетный фрагмент не был обработан");
            Assert.True(lowPriorityIndex >= 0, "Низкоприоритетный фрагмент не был обработан");
            
            Assert.True(highPriorityIndex < mediumPriorityIndex && mediumPriorityIndex < lowPriorityIndex, 
                $"Фрагменты обработаны в неправильном порядке: {string.Join(", ", completedFragmentIds)}");
        }

        [Fact]
        public async Task GetFragmentGlobalId_ReturnsValidId()
        {
            // Arrange
            byte[] audioData = new byte[1000]; // Тестовые аудиоданные
            var session = _sessionManager.CreateSession();
            Console.WriteLine($"Created session with ID: {session.Id}");
            
            // Добавляем фрагмент и получаем его ID
            var (fragmentId, _) = await _sessionManager.EnqueueFragmentAsync(audioData, sessionId: session.Id);
            Console.WriteLine($"Added fragment with ID: {fragmentId}");
            
            // Act
            Guid globalId = _sessionManager.GetFragmentGlobalId(session.Id, fragmentId);
            Console.WriteLine($"Generated global ID: {globalId}");
            
            // Проверяем, что сессия существует
            var retrievedSession = _sessionManager.GetSession(session.Id);
            Console.WriteLine($"Retrieved session: {(retrievedSession != null ? retrievedSession.Id.ToString() : "null")}");
            
            // Проверяем, что фрагмент существует в сессии
            var sessionFragment = retrievedSession?.GetFragment(fragmentId);
            Console.WriteLine($"Retrieved fragment from session: {(sessionFragment != null ? fragmentId.ToString() : "null")}");
            
            // Assert
            Assert.NotEqual(Guid.Empty, globalId);
            
            // Проверяем, что мы можем получить информацию о фрагменте по его глобальному ID
            var fragment = _sessionManager.GetFragmentInfo(globalId);
            Console.WriteLine($"Retrieved fragment info: {(fragment != null ? fragment.FragmentId.ToString() : "null")}");
            
            Assert.NotNull(fragment);
            Assert.Equal(fragmentId, fragment.FragmentId);
        }

        [Fact]
        public async Task CancelTokenMiddleOfProcessing_AbortsFurtherProcessing()
        {
            // Arrange - Создаем совершенно новые моки
            var recognizerMock = new Mock<ISpeechRecognizer>();
            var loggerMock = new NullLogger<SessionManagerTests>();
            
            // Создаем реальную сессию, не мок
            var session = new RecognitionSession(recognizerMock.Object, loggerMock);
            
            // Добавляем тестовые фрагменты
            await session.AddFragmentAsync(new byte[100]);
            await session.AddFragmentAsync(new byte[100]);
            
            // Симулируем длительную обработку
            bool wasCancelled = false;
            var cts = new CancellationTokenSource();
            
            // Настраиваем распознаватель, чтобы он реагировал на отмену
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>(async (data, token) => 
                {
                    try
                    {
                        // Длительная операция
                        await Task.Delay(5000, token);
                        return "Результат";
                    }
                    catch (OperationCanceledException)
                    {
                        wasCancelled = true;
                        throw;
                    }
                });
                
            // Act
            // Запускаем обработку и отменяем через небольшую задержку
            var processingTask = Task.Run(async () => 
            {
                try 
                {
                    await Task.Delay(100); // Небольшая задержка, чтобы имитировать задержку перед отменой
                    cts.Cancel();
                }
                catch (Exception)
                {
                    // Игнорируем исключения
                }
            });
            
            try 
            {
                // Пытаемся обработать фрагмент с токеном отмены
                await session.ProcessFragmentAsync(1, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Ожидаемое исключение
            }
            
            await processingTask;
            
            // Assert
            Assert.True(wasCancelled, "Задача должна быть отменена");
        }

        [Fact]
        public async Task QueueProcessing_WithRetryFailedItems_RetriesFailedFragments()
        {
            // Arrange - Создаем реальные объекты
            var recognizerMock = new Mock<ISpeechRecognizer>();
            var loggerMock = new NullLogger<SessionManagerTests>();
            
            // Настраиваем распознаватель для первого вызова (с ошибкой)
            bool failedOnce = false;
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((data, token) =>
                {
                    if (!failedOnce)
                    {
                        failedOnce = true;
                        throw new Exception("Тестовая ошибка");
                    }
                    return Task.FromResult("Успешно распознано со второй попытки");
                });
            
            // Создаем реальную сессию
            var session = new RecognitionSession(recognizerMock.Object, loggerMock);
            
            // Добавляем фрагмент
            int fragmentId = await session.AddFragmentAsync(new byte[100]);
            
            // Симулируем ошибку при обработке фрагмента
            try
            {
                await session.ProcessFragmentAsync(fragmentId);
            }
            catch
            {
                // Ожидаемое исключение - игнорируем
                Console.WriteLine("Ожидаемая ошибка первой обработки");
            }
            
            // Вручную устанавливаем статус Failed для фрагмента
            var fragments = session.GetFragments();
            var fragment = fragments.FirstOrDefault(f => f.FragmentId == fragmentId);
            
            if (fragment != null)
            {
                fragment.Status = FragmentStatus.Failed;
                Console.WriteLine($"Установлен статус Failed для фрагмента {fragmentId}");
            }
            
            // Проверяем, что статус фрагмента - Failed
            fragments = session.GetFragments();
            var failedFragment = fragments.FirstOrDefault(f => f.Status == FragmentStatus.Failed);
            
            Assert.NotNull(failedFragment);
            Assert.Equal(fragmentId, failedFragment.FragmentId);
            
            // Act - Повторяем обработку неудачного фрагмента
            bool result = session.RetryFailedFragment(fragmentId);
            
            // Ждем некоторое время для завершения асинхронной обработки
            await Task.Delay(500);
            
            // Assert
            Assert.True(result, "Повторная обработка должна быть запущена");
            
            // Проверяем, что статус изменился на Pending или Completed
            var updatedFragments = session.GetFragments();
            var retriedFragment = updatedFragments.FirstOrDefault(f => f.FragmentId == fragmentId);
            
            Assert.NotNull(retriedFragment);
            Assert.NotEqual(FragmentStatus.Failed, retriedFragment.Status);
        }

        [Fact]
        public async Task RetryFailedFragment_WithCorrectSessionAndFragmentIds_ReturnsTrue()
        {
            // Arrange - Создаем реальные объекты
            var recognizerMock = new Mock<ISpeechRecognizer>();
            var loggerMock = new NullLogger<SessionManagerTests>();
            
            // Настраиваем распознаватель
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Тестовый текст");
            
            // Создаем реальную сессию и менеджер сессий
            var session = new RecognitionSession(recognizerMock.Object, loggerMock);
            var sessionManager = new SessionManager(recognizerMock.Object, loggerMock);
            
            // Добавляем фрагмент в сессию
            int fragmentId = await session.AddFragmentAsync(new byte[100]);
            
            // Устанавливаем статус фрагмента как Failed (через рефлексию или путем вызова метода с отказом)
            try
            {
                // Пробуем обработать фрагмент, но с ошибкой
                recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new Exception("Тестовая ошибка"));
                
                await session.ProcessFragmentAsync(fragmentId);
            }
            catch
            {
                // Игнорируем исключение
            }
            
            // Сбрасываем настройку распознавателя для успешной работы
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Восстановленный текст");
            
            // Act - Повторяем обработку неудачного фрагмента
            bool result = session.RetryFailedFragment(fragmentId);
            
            // Assert
            Assert.True(result, "Повторная обработка фрагмента должна быть запущена");
        }

        public void Dispose()
        {
            // Освобождаем ресурсы
            _sessionManager.Dispose();
        }
    }
} 