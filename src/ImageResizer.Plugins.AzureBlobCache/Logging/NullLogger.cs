using ImageResizer.Configuration.Logging;
using System;

namespace ImageResizer.Plugins.AzureBlobCache.Logging
{
    public class NullLogger : ILogger
    {
        public string LoggerName { get => nameof(NullLogger); set => throw new NotSupportedException("Null logger name isn't settable"); }

        public bool IsTraceEnabled => false;
        public bool IsDebugEnabled => false;
        public bool IsInfoEnabled => false;
        public bool IsWarnEnabled => false;
        public bool IsErrorEnabled => false;
        public bool IsFatalEnabled => false;

        public void Log(string level, string message) { /* Do nothing */ }
        public void Trace(string message) { /* Do nothing */ }
        public void Trace(string message, params object[] args) { /* Do nothing */ }
        public void Debug(string message) { /* Do nothing */ }
        public void Debug(string message, params object[] args) { /* Do nothing */ }
        public void Info(string message) { /* Do nothing */ }
        public void Info(string message, params object[] args) { /* Do nothing */ }
        public void Warn(string message) { /* Do nothing */ }
        public void Warn(string message, params object[] args) { /* Do nothing */ }
        public void Error(string message) { /* Do nothing */ }
        public void Error(string message, params object[] args) { /* Do nothing */ }
        public void Fatal(string message) { /* Do nothing */ }
        public void Fatal(string message, params object[] args) { /* Do nothing */ }

        public bool IsEnabled(string level)
        {
            return false;
        }
    }
}
