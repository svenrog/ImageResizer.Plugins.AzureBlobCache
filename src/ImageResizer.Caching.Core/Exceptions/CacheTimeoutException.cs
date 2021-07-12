using System;

namespace ImageResizer.Caching.Core.Exceptions
{
    /// <summary>
    /// Indicates a problem with caching
    /// </summary>
    public class CacheTimeoutException : Exception
    {
        public CacheTimeoutException(string message) : base(message) { }
        public CacheTimeoutException(string message, Exception innerException) : base(message, innerException) { }
    }
}
