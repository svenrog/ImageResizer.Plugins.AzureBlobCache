using ImageResizer.Caching.Core;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheQueryResult : ICacheResult
    {
        public CacheQueryResult Result { get; set; }
        public byte[] Contents { get; set; }
    }
}
