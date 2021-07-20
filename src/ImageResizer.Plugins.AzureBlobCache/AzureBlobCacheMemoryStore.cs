using ImageResizer.Caching.Core;
using System;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheMemoryStore : ICacheStore
    {
        private const string _name = nameof(AzureBlobCacheMemoryStore);
        private readonly TimeSpan? _absoluteExpiry;
        private readonly TimeSpan? _slidingExpiry;
        
        protected readonly MemoryCache Store;

        public AzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, TimeSpan? pollingInterval = null) : this(cacheMemoryLimitMb, null, absoluteExpiry, slidingExpiry, pollingInterval) { }
        public AzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, int? physicalMemoryLimitPercentage = null, TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, TimeSpan? pollingInterval = null)
        {
            _absoluteExpiry = absoluteExpiry;
            _slidingExpiry = slidingExpiry;

            var config = GetConfig(cacheMemoryLimitMb, physicalMemoryLimitPercentage, pollingInterval ?? TimeSpan.FromMinutes(4));
            Store = new MemoryCache(_name, config);
        }

        public ICacheResult Get(Guid key)
        {
            var cacheKey = GetCacheKey(key);
            var cacheItem = Store.Get(cacheKey);

            return cacheItem as ICacheResult;
        }

        public void Insert(Guid key, ICacheResult value)
        {
            var cacheKey = GetCacheKey(key);
            var cacheItem = new CacheItem(cacheKey, value);
            
            var absoluteExpiration = _absoluteExpiry.HasValue ? new DateTimeOffset(DateTime.UtcNow.Add(_absoluteExpiry.Value)) : default;
            var slidingExpiration = _slidingExpiry ?? default;

            if (_absoluteExpiry.HasValue && _slidingExpiry.HasValue)
                slidingExpiration = default;

            var policy = new CacheItemPolicy
            {
                Priority = CacheItemPriority.Default,
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpiration = slidingExpiration
            };

            Store.Add(cacheItem, policy);
        }

        private string GetCacheKey(Guid key)
        {
            return $"{key:D}";
        }

        private NameValueCollection GetConfig(int? cacheMemoryLimitMb, int? physicalMemoryLimitPercentage, TimeSpan pollingInterval)
        {
            var config = new NameValueCollection(3);

            if (cacheMemoryLimitMb.HasValue)
                config.Add("CacheMemoryLimitMegabytes", cacheMemoryLimitMb.Value.ToString());

            if (physicalMemoryLimitPercentage.HasValue)
                config.Add("PhysicalMemoryLimitPercentage", physicalMemoryLimitPercentage.Value.ToString());

            config.Add("PollingInterval", pollingInterval.ToString("hh':'mm':'ss"));

            return config;
        }
    }
}
