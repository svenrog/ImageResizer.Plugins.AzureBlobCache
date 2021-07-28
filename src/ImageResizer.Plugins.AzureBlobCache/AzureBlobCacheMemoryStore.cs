using ImageResizer.Caching.Core;
using ImageResizer.Configuration.Logging;
using ImageResizer.Plugins.AzureBlobCache.Extensions;
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
        private readonly ILoggerProvider _log;
        
        protected readonly MemoryCache Store;

        public AzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, TimeSpan? pollingInterval = null, ILoggerProvider loggerProvider = null) : this(cacheMemoryLimitMb, null, absoluteExpiry, slidingExpiry, pollingInterval, loggerProvider) { }
        public AzureBlobCacheMemoryStore(int? cacheMemoryLimitMb, int? physicalMemoryLimitPercentage = null, TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, TimeSpan? pollingInterval = null, ILoggerProvider loggerProvider = null)
        {
            _absoluteExpiry = absoluteExpiry;
            _slidingExpiry = slidingExpiry;
            _log = loggerProvider;

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
            
            var absoluteExpiration = _absoluteExpiry.HasValue ? new DateTimeOffset(DateTime.UtcNow.Add(_absoluteExpiry.Value)) : DateTimeOffset.MaxValue;
            var slidingExpiration = _slidingExpiry ?? TimeSpan.Zero;

            if (_absoluteExpiry.HasValue && _slidingExpiry.HasValue)
                slidingExpiration = TimeSpan.Zero;

            var policy = new CacheItemPolicy
            {
                Priority = CacheItemPriority.Default,
                AbsoluteExpiration = absoluteExpiration,
                SlidingExpiration = slidingExpiration
            };

            if (_log.IsDebugEnabled())
                _log.Debug($"Inserting item with key '{key:D}' into memory cache.");

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
