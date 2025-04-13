using System;
using Microsoft.Extensions.Logging;
using SpeechRecognition.Core.Audio;
using SpeechRecognition.Core.Models;

namespace SpeechRecognition.Core.Recognition
{
    /// <summary>
    /// Фабрика для создания распознавателей речи
    /// </summary>
    public static class SpeechRecognizerFactory
    {
        /// <summary>
        /// Создает распознаватель речи на основе настроек модели
        /// </summary>
        /// <param name="audioProcessor">Процессор аудио</param>
        /// <param name="modelSettings">Настройки модели</param>
        /// <param name="logger">Логгер для пользовательских реализаций</param>
        /// <returns>Распознаватель речи</returns>
        public static ISpeechRecognizer CreateRecognizer(IAudioProcessor audioProcessor, IModelSettings modelSettings, ILogger logger = null)
        {
            if (audioProcessor == null)
                throw new ArgumentNullException(nameof(audioProcessor));
            
            if (modelSettings == null)
                throw new ArgumentNullException(nameof(modelSettings));

            return modelSettings.RecognizerType switch
            {
                RecognizerType.Whisper => new WhisperRecognizer(audioProcessor, modelSettings, logger ?? throw new ArgumentException("Для Whisper-распознавателя требуется указать логгер", nameof(logger))),
                RecognizerType.Mock => modelSettings is MockModelSettings mockSettings 
                    ? new MockSpeechRecognizer(audioProcessor, modelSettings, mockSettings.MockResponse)
                    : throw new ArgumentException("Неверный тип настроек для Mock-распознавателя", nameof(modelSettings)),
                RecognizerType.Custom => modelSettings is CustomModelSettings
                    ? logger != null
                        ? new CustomSpeechRecognizer(audioProcessor, modelSettings, logger)
                        : throw new ArgumentException("Для пользовательской модели требуется указать логгер", nameof(logger))
                    : throw new ArgumentException("Неверный тип настроек для пользовательской модели", nameof(modelSettings)),
                _ => throw new ArgumentException($"Неподдерживаемый тип распознавателя: {modelSettings.RecognizerType}")
            };
        }
    }
} 