using System;
using System.Runtime.Caching;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCacheMemoryStore : AzureBlobCacheMemoryStore
    {
        public TestableAzureBlobCacheMemoryStore(int cacheMemoryLimitMb, TimeSpan? absoluteExpiry, TimeSpan? slidingExpiry, TimeSpan? pollingInterval = null) : base(cacheMemoryLimitMb, absoluteExpiry, slidingExpiry, pollingInterval)
        {

        }

        public virtual MemoryCache InternalStore => Store;
    }
}
