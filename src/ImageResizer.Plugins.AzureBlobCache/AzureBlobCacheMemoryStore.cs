using ImageResizer.Caching.Core;
using System;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheMemoryStore : ICacheStore
    {
        private const string _name = nameof(AzureBlobCacheMemoryStore);
        protected readonly MemoryCache Store;

        public AzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, string pollingInterval = "00:04:01", int? physicalMemoryLimitPercentage = null)
        {
            var config = GetConfig(cacheMemoryLimitMb, physicalMemoryLimitPercentage, pollingInterval);

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
            var expiration = new DateTimeOffset(DateTime.UtcNow.AddHours(1));
            var policy = new CacheItemPolicy
            {
                Priority = CacheItemPriority.Default,
                AbsoluteExpiration = expiration
            };

            Store.Add(cacheItem, policy);
        }

        private string GetCacheKey(Guid key)
        {
            return $"{key:D}";
        }

        private NameValueCollection GetConfig(int? cacheMemoryLimitMb, int? physicalMemoryLimitPercentage, string pollingInterval)
        {
            var config = new NameValueCollection(3);

            if (cacheMemoryLimitMb.HasValue)
                config.Add("CacheMemoryLimitMegabytes", cacheMemoryLimitMb.Value.ToString());

            if (physicalMemoryLimitPercentage.HasValue)
                config.Add("PhysicalMemoryLimitPercentage", physicalMemoryLimitPercentage.Value.ToString());

            if (!string.IsNullOrEmpty(pollingInterval))
                config.Add("PollingInterval", pollingInterval);

            return config;
        }
    }
}
