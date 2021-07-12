using ImageResizer.Caching.Core;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Models
{
    public class TestCacheResult : ICacheResult
    {
        public CacheQueryResult Result { get; set; }
        public byte[] Contents { get; set; }
    }
}
