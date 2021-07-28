using ImageResizer.Configuration.Logging;
using System;

namespace ImageResizer.Plugins.AzureBlobCache.Extensions
{
    public static class LoggerProviderExtensions
    {
        public static bool IsTraceEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsTraceEnabled ?? false;
        }

        public static bool IsDebugEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsDebugEnabled ?? false;
        }

        public static bool IsInfoEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsInfoEnabled ?? false;
        }

        public static bool IsWarnEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsWarnEnabled ?? false;
        }

        public static bool IsErrorEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsErrorEnabled ?? false;
        }

        public static bool IsFatalEnabled(this ILoggerProvider provider)
        {
            return provider?.Logger?.IsFatalEnabled ?? false;
        }

        public static bool IsEnabled(this ILoggerProvider provider, string level)
        {
            return provider?.Logger.IsEnabled(level) ?? false;
        }

        public static void Log(this ILoggerProvider provider, string level, string message) 
        {
            provider?.Logger?.Log(level, message);
        }

        public static void Trace(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Trace(message);
        }

        public static void Trace(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Trace(message, args);
        }

        public static void Debug(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Debug(message);
        }

        public static void Debug(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Debug(message, args);
        }

        public static void Info(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Info(message);
        }

        public static void Info(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Info(message, args);
        }

        public static void Warn(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Warn(message);
        }

        public static void Warn(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Warn(message, args);
        }

        public static void Error(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Error(message);
        }

        public static void Error(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Error(message, args);
        }

        public static void Error(this ILoggerProvider provider, Exception exception)
        {
            provider?.Logger?.Error(FormatException(exception));
        }

        public static void Error(this ILoggerProvider provider, string message, Exception exception)
        {
            provider?.Logger?.Error(FormatException(message, exception));
        }

        public static void Fatal(this ILoggerProvider provider, string message)
        {
            provider?.Logger?.Fatal(message);
        }

        public static void Fatal(this ILoggerProvider provider, string message, params object[] args)
        {
            provider?.Logger?.Fatal(message, args);
        }

        public static void Fatal(this ILoggerProvider provider, Exception exception)
        {
            provider?.Logger?.Fatal(FormatException(exception));
        }

        public static void Fatal(this ILoggerProvider provider, string message, Exception exception)
        {
            provider?.Logger?.Fatal(FormatException(message, exception));
        }

        private static string FormatException(string message, Exception exception)
        {
            return $"{message}: {exception.Message}{Environment.NewLine}{exception.StackTrace}";
        }

        private static string FormatException(Exception exception)
        {
            return $"{exception.Message}{Environment.NewLine}{exception.StackTrace}";
        }
    }
}
