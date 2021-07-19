using System;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Exceptions
{
    public class ImageFormatValidationException : Exception
    {
        public ImageFormatValidationException(string message) : base(message) { }
    }
}
