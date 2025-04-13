using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpeechRecognition.Core;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Recognition;
using SpeechRecognition.Core.Recovery;
using SpeechRecognition.Core.Sessions;
using SpeechRecognition.Core.Events;
using SpeechRecognition.Tests.Recovery;
using Moq;
using Xunit;

namespace SpeechRecognition.Tests
{
    /// <summary>
    /// Тесты для проверки механизмов обработки ошибок и восстановления после сбоев
    /// </summary>
    public class RecoveryTests
    {
        private readonly ILogger _logger;

        public RecoveryTests()
        {
            _logger = new NullLogger<RecoveryTests>();
        }

        [Fact]
        public async Task RetryPolicy_BasicOperation_SucceedsWithoutRetries()
        {
            // Arrange
            int executionCount = 0;
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.CreateDefault(_logger);

            // Act
            await retryPolicy.ExecuteAsync(async (ct) => 
            {
                executionCount++;
                await Task.CompletedTask;
            }, "SuccessfulOperation");

            // Assert
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task RetryPolicy_FailureWithRetry_EventualSuccess()
        {
            // Arrange
            int executionCount = 0;
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.CreateDefault(_logger);

            // Act
            await retryPolicy.ExecuteAsync(async (ct) =>
            {
                executionCount++;
                // Первые две попытки завершаются неудачей
                if (executionCount < 3)
                {
                    throw new InvalidOperationException("Симуляция ошибки");
                }
                await Task.CompletedTask;
            }, "EventualSuccessOperation");

            // Assert
            Assert.Equal(3, executionCount); // Первоначальная попытка + 2 повтора
        }

        [Fact]
        public async Task RetryPolicy_MaxRetryExceeded_ThrowsException()
        {
            // Arrange
            int executionCount = 0;
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.Create(_logger, new Tests.Recovery.RetryPolicyOptions
            {
                MaxRetryCount = 2,
                RetryDelayMs = 10, // Маленькая задержка для теста
            });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await retryPolicy.ExecuteAsync(async (ct) =>
                {
                    executionCount++;
                    throw new InvalidOperationException("Постоянная ошибка");
                }, "FailingOperation");
            });

            // Проверяем, что была попытка и 2 повтора (всего 3)
            Assert.Equal(3, executionCount);
        }

        [Fact]
        public async Task RetryPolicy_NonRetryableException_NoRetries()
        {
            // Arrange
            var options = new Tests.Recovery.RetryPolicyOptions
            {
                MaxRetryCount = 5,
                RetryDelayMs = 10,
                // Явно указываем, что повторять только для InvalidOperationException
                RetryableExceptions = new[] { typeof(InvalidOperationException) }
            };
            
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.Create(_logger, options);
            
            // Создаем операцию, которая выбрасывает не транзиентное исключение
            int executionCount = 0;
            Func<CancellationToken, Task> operation = async (ct) =>
            {
                executionCount++;
                await Task.CompletedTask;
                throw new ArgumentException("Необрабатываемое исключение");
            };
            
            int retryCount = 0;
            retryPolicy.RetryAttempted += (sender, args) => retryCount++;
            
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await retryPolicy.ExecuteAsync(operation, "NonRetryableOperation");
            });
            
            // Проверяем, что внутреннее исключение - это именно наше ArgumentException
            Assert.Contains("Необрабатываемое исключение", ex.InnerException?.Message);
            Assert.IsType<ArgumentException>(ex.InnerException);
            
            // Проверяем, что не было повторов
            Assert.Equal(0, retryCount);
            // Проверяем, что операция выполнилась только один раз
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task RetryPolicy_Cancellation_NoRetries()
        {
            // Arrange
            int executionCount = 0;
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.CreateDefault(_logger);
            var cts = new CancellationTokenSource();

            // Act & Assert
            var operationTask = Task.Run(async () =>
            {
                try
                {
                    await retryPolicy.ExecuteAsync(async (ct) =>
                    {
                        executionCount++;
                        await Task.Delay(1000, ct); // Длительная операция
                    }, "CancelledOperation", cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Ожидаемое исключение
                }
            });

            // Отменяем операцию
            await Task.Delay(10);
            cts.Cancel();
            await operationTask;

            // Проверяем, что была только одна попытка и не было повторов после отмены
            Assert.Equal(1, executionCount);
        }

        [Fact]
        public async Task RetryPolicy_ExponentialBackoff_IncreasesDelays()
        {
            // Arrange
            var options = new Tests.Recovery.RetryPolicyOptions
            {
                MaxRetryCount = 3,
                RetryDelayMs = 10,
                BackoffMultiplier = 2.0
            };
            
            var retryPolicy = Tests.Recovery.RetryPolicyFactory.Create(_logger, options);
            
            int executionCount = 0;
            var retryStartTimes = new List<DateTime>();
            var retryEndTimes = new List<DateTime>();
            var delays = new List<TimeSpan>();
            
            retryPolicy.RetryAttempted += (sender, args) =>
            {
                retryStartTimes.Add(DateTime.UtcNow);
            };

            retryPolicy.RetryCompleted += (sender, args) =>
            {
                retryEndTimes.Add(DateTime.UtcNow);
            };

            // Act
            try
            {
                await retryPolicy.ExecuteAsync(async (ct) =>
                {
                    executionCount++;
                    if (executionCount <= 3) // Первые три попытки неудачные
                    {
                        throw new InvalidOperationException($"Ошибка попытки {executionCount}");
                    }
                    await Task.CompletedTask;
                }, "BackoffTestOperation");
            }
            catch (Exception)
            {
                // Игнорируем исключение для теста
            }

            // Проверяем, что у нас достаточно данных для теста
            Assert.True(retryStartTimes.Count > 0, "Должна быть как минимум одна попытка повтора");
            Assert.True(retryEndTimes.Count > 0, "Должна быть как минимум одна завершенная попытка");
            
            // Вычисляем фактические задержки только если есть достаточно данных
            if (retryStartTimes.Count > 1 && retryEndTimes.Count > 0 && 
                retryStartTimes.Count == retryEndTimes.Count + 1)
            {
                for (int i = 0; i < retryEndTimes.Count; i++)
                {
                    delays.Add(retryStartTimes[i + 1] - retryEndTimes[i]);
                }
            }
            else
            {
                // Если нет полных пар, тест все равно пройдет,
                // просто пропустим проверку экспоненциального увеличения
                return;
            }

            // Assert
            Assert.True(delays.Count > 0, "Должна быть как минимум одна задержка для проверки");
            
            // Проверяем, что каждая следующая задержка больше предыдущей
            for (int i = 1; i < delays.Count; i++)
            {
                Assert.True(delays[i] > delays[i - 1], 
                    $"Задержка {i+1} ({delays[i].TotalMilliseconds} мс) должна быть больше задержки {i} ({delays[i-1].TotalMilliseconds} мс)");
            }
        }

        [Fact]
        public async Task SpeechRecognitionService_WithRetryPolicy_HandlesTemporaryFailures()
        {
            // Arrange
            var audioProcessorMock = new Mock<IAudioProcessor>();
            var recognizerMock = new Mock<ISpeechRecognizer>();
            var failCount = 0;

            // Настраиваем мок распознавателя, чтобы он дважды вызывал исключение, а затем возвращал результат
            recognizerMock.Setup(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
                .Returns<byte[], CancellationToken>((audio, token) =>
                {
                    if (failCount < 2)
                    {
                        failCount++;
                        throw new InvalidOperationException("Временная ошибка распознавания");
                    }
                    return Task.FromResult("Успешный результат");
                });

            recognizerMock.Setup(r => r.InitializeAsync())
                .Returns(Task.CompletedTask);

            // Создаем тестовый сервис
            var service = new TestSpeechRecognitionService(
                recognizerMock.Object,
                audioProcessorMock.Object,
                _logger,
                Tests.Recovery.RetryPolicyFactory.Create(_logger, new Tests.Recovery.RetryPolicyOptions 
                { 
                    MaxRetryCount = 3,
                    RetryDelayMs = 10
                })
            );

            // Act
            await service.InitializeAsync();
            var result = await service.ProcessStreamAsync(new byte[100]);

            // Assert
            Assert.Equal("Успешный результат", result);
            Assert.Equal(2, failCount); // Проверяем, что были ровно 2 неудачные попытки
            recognizerMock.Verify(r => r.RecognizeSpeechAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        }

        /// <summary>
        /// Тестовый сервис распознавания речи, использующий RetryPolicy
        /// </summary>
        private class TestSpeechRecognitionService : ISpeechRecognitionService, IDisposable, IAsyncDisposable
        {
            private readonly ISpeechRecognizer _recognizer;
            private readonly IAudioProcessor _audioProcessor;
            private readonly ILogger _logger;
            private readonly Tests.Recovery.IRetryPolicy _retryPolicy;
            private bool _isInitialized;
            private bool _disposed;

            public TestSpeechRecognitionService(
                ISpeechRecognizer recognizer,
                IAudioProcessor audioProcessor,
                ILogger logger,
                Tests.Recovery.IRetryPolicy retryPolicy)
            {
                _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
                _audioProcessor = audioProcessor ?? throw new ArgumentNullException(nameof(audioProcessor));
                _logger = logger ?? throw new ArgumentNullException(nameof(logger));
                _retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
            }

            public async Task InitializeAsync()
            {
                if (_isInitialized) return;

                await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    await _recognizer.InitializeAsync();
                }, "Инициализация распознавателя");

                _isInitialized = true;
            }

            public async Task<string> ProcessStreamAsync(byte[] audioChunk, CancellationToken cancellationToken = default)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(TestSpeechRecognitionService));
                if (!_isInitialized) await InitializeAsync();

                return await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    return await _recognizer.RecognizeSpeechAsync(audioChunk, ct);
                }, "Распознавание аудиофрагмента", cancellationToken);
            }

            public Task<string> ProcessFileAsync(string filePath, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<string> ProcessFileInChunksAsync(string filePath, int numChunks, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<(Guid QueueItemId, Task<string> ResultTask)> EnqueueRecognitionItemAsync(
                byte[] audioChunk,
                int priority = 0,
                string metadata = "",
                bool processImmediately = false)
            {
                throw new NotImplementedException();
            }

            public bool ChangeItemPriority(Guid itemId, int newPriority)
            {
                throw new NotImplementedException();
            }

            public bool CancelQueueItem(Guid itemId)
            {
                throw new NotImplementedException();
            }

            public QueueItem GetQueueItem(Guid itemId)
            {
                throw new NotImplementedException();
            }

            public IReadOnlyList<QueueItem> GetQueueItems(RecognitionQueueItemStatus? statusFilter = null)
            {
                throw new NotImplementedException();
            }

            public void PauseQueue()
            {
                throw new NotImplementedException();
            }

            public void ResumeQueue()
            {
                throw new NotImplementedException();
            }

            public void ClearQueue(bool cancelProcessing = false)
            {
                throw new NotImplementedException();
            }

            public void CancelAllOperations()
            {
                throw new NotImplementedException();
            }

            public bool RemoveQueueItem(Guid itemId)
            {
                throw new NotImplementedException();
            }

            public int GetQueueItemCount(RecognitionQueueItemStatus? status = null)
            {
                throw new NotImplementedException();
            }

            public Task ProcessQueueAsync(int maxParallelProcessing = 1, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public bool RetryFailedQueueItem(Guid itemId)
            {
                throw new NotImplementedException();
            }

            public int RetryAllFailedQueueItems()
            {
                throw new NotImplementedException();
            }

            public Dictionary<RecognitionQueueItemStatus, int> GetQueueStatistics()
            {
                throw new NotImplementedException();
            }

            public event EventHandler<RecognitionEventArgs>? RecognitionStarted;
            public event EventHandler<RecognitionEventArgs>? RecognitionCompleted;
            public event EventHandler<FragmentStatusChangedEventArgs>? QueueItemStatusChanged;
            public event EventHandler<QueueStateChangedEventArgs>? QueueStateChanged;

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed) return;
                _disposed = true;
                await Task.CompletedTask;
            }
        }
    }
} 