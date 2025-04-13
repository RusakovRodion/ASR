using System;
using System.Text;
using Microsoft.Extensions.Logging;

namespace SpeechRecognition.Tests
{
    /// <summary>
    /// Вспомогательный класс для логирования в тестах
    /// </summary>
    public static class TestLogger
    {
        static TestLogger()
        {
            // Устанавливаем кодировку UTF-8 для вывода в консоль
            Console.OutputEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// Логирует информационное сообщение
        /// </summary>
        public static void LogInformation(string message)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"[INFO] {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Логирует предупреждение
        /// </summary>
        public static void LogWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[WARNING] {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Логирует ошибку
        /// </summary>
        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] {message}");
            Console.ResetColor();
        }
    }
} 