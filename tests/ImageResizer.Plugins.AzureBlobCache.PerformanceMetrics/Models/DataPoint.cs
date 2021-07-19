using ImageResizer.Caching.Core;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class DataPoint
    {
        public CacheQueryResult Result { get; set; }
        public float Processor { get; set; }
        public float Memory { get; set; }
        public long StartedMilliseconds { get; set; }
        public long ElapsedMilliseconds { get; set; }
    }
}