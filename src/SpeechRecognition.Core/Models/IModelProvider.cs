using System.Threading.Tasks;

namespace SpeechRecognition.Core.Models
{
    /// <summary>
    /// Интерфейс провайдера моделей для распознавания речи
    /// </summary>
    public interface IModelProvider
    {
        /// <summary>
        /// Получает тип распознавателя, с которым работает провайдер
        /// </summary>
        RecognizerType RecognizerType { get; }
        
        /// <summary>
        /// Проверяет наличие модели и загружает её при необходимости
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>Путь к загруженной модели</returns>
        Task<string> EnsureModelExistsAsync(IModelSettings modelSettings);
        
        /// <summary>
        /// Проверяет совместимость настроек с данным провайдером
        /// </summary>
        /// <param name="modelSettings">Настройки модели</param>
        /// <returns>true, если настройки совместимы</returns>
        bool IsCompatible(IModelSettings modelSettings);
    }
} 