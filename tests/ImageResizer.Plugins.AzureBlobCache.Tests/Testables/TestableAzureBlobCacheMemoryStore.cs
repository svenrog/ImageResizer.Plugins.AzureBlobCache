using System;
using System.Runtime.Caching;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCacheMemoryStore : AzureBlobCacheMemoryStore
    {
        public TestableAzureBlobCacheMemoryStore(int cacheMemoryLimitMb, TimeSpan absoluteExpiry, TimeSpan? pollingInterval = null) : base(cacheMemoryLimitMb, null, absoluteExpiry, null, pollingInterval)
        {

        }

        public virtual MemoryCache InternalStore => Store;
    }
}
