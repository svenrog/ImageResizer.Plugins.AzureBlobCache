using System.Runtime.Caching;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCacheMemoryStore : AzureBlobCacheMemoryStore
    {
        public TestableAzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, string pollingInterval = "00:04:01", int? physicalMemoryLimitPercentage = null) : base(cacheMemoryLimitMb, pollingInterval, physicalMemoryLimitPercentage)
        {

        }

        public virtual MemoryCache InternalStore => Store;
    }
}
