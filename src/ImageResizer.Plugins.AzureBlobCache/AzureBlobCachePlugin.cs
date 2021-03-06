using ImageResizer.Caching;
using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Identity;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Operation;
using ImageResizer.Configuration;
using ImageResizer.Configuration.Logging;
using ImageResizer.Plugins.AzureBlobCache.Handlers;
using ImageResizer.Plugins.Basic;
using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCachePlugin : IAsyncTyrantCache, ICache, IPlugin, ILoggerProvider
    {
        private volatile bool _started = false;

        protected string ConnectionName = "ResizerAzureBlobs";
        protected string ContainerName = "imagecache";
        protected int TimeoutSeconds = 5;

        protected int MemoryStoreLimitMb;
        protected string MemoryStorePollingInterval = "00:04:01";
        protected string MemoryStoreSlidingExpiration = "00:30:00";
        protected string MemoryStoreAbsoluteExpiration = null;

        protected int IndexMaxSizeMb;
        protected int IndexMaxItems;
        protected string IndexPollingInterval = "00:05:00";

        protected int ClientCacheMinutes;
        protected bool LoggingEnabled;

        private IAsyncCacheProvider _cacheProvider;
        private ICacheKeyGenerator _keyGenerator;
        private ILogger _logger;

        public virtual ILogger Logger => _logger;

        public virtual async Task ProcessAsync(HttpContext context, IAsyncResponsePlan plan)
        {
            using (var tokenSource = GetTokenSource())
            {
                var operationTiming = new Stopwatch();

                operationTiming.Start();

                var key = _keyGenerator.Generate(plan.RequestCachingKey, plan.EstimatedFileExtension);
                var cacheResult = await _cacheProvider.GetAsync(key, tokenSource.Token);

                if (cacheResult.Result == CacheQueryResult.Miss)
                {
                    cacheResult = await _cacheProvider.CreateAsync(key, tokenSource.Token, (stream) => plan.CreateAndWriteResultAsync(stream, plan));
                }

                operationTiming.Stop();

                if (cacheResult.Result == CacheQueryResult.Failed && (_logger?.IsWarnEnabled ?? false))
                {
                    _logger.Warn($"Timeout result for key '{key:D}' and resizer key '{plan.RequestCachingKey}', has content: {cacheResult.Contents != null}, operation took: {operationTiming.ElapsedMilliseconds} ms.");
                }
                else if (_logger?.IsDebugEnabled ?? false)
                {
                    _logger.Debug($"Cache result '{cacheResult.Result}' for key '{key:D}' and resizer key '{plan.RequestCachingKey}', operation took: {operationTiming.ElapsedMilliseconds} ms");
                }                    

                // Note: Since the async pipleine doesn't have support for client caching, we're patching it here.
                // https://github.com/imazen/resizer/issues/166
                // This was written 2021-07-13
                PreHandleImage(context);

                if (cacheResult.Contents != null)
                {
                    RemapContentResponse(context, plan, cacheResult.Contents);
                }
                else
                {
                    RemapMissResponse(context, plan);
                }
            }
        }

        public virtual bool CanProcess(HttpContext context, IAsyncResponsePlan plan)
        {
            var instructions = new Instructions(plan.RewrittenQuerystring);
            if (instructions.Cache == ServerCacheMode.No) return false;

            return _started;
        }

        public IPlugin Install(Config config)
        {
            LoadSettings(config);
            ConfigureLogger(config);

            Start();

            config.Plugins.add_plugin(this);

            return this;
        }

        public bool Uninstall(Config config)
        {
            config.Plugins.remove_plugin(this);

            Stop();

            return true;
        }

        /// <summary>
        /// Uses the defaults from the resizing.azureBlobCache section in the specified configuration.
        /// </summary>
        public virtual void LoadSettings(Config config)
        {
            ConnectionName = config.get("azureBlobCache.connectionName", ConnectionName);
            ContainerName = config.get("azureBlobCache.containerName", ContainerName);
            TimeoutSeconds = config.get("azureBlobCache.timeoutSeconds", TimeoutSeconds);
            LoggingEnabled = config.get("azureBlobCache.logging", LoggingEnabled);
            IndexMaxSizeMb = config.get("azureBlobCache.indexMaxSizeMb", IndexMaxSizeMb);
            IndexMaxItems = config.get("azureBlobCache.indexMaxItems", IndexMaxItems);
            IndexPollingInterval = config.get("azureBlobCache.indexPollingInterval", IndexPollingInterval);
            MemoryStoreLimitMb = config.get("azureBlobCache.memoryStoreLimitMb", MemoryStoreLimitMb);
            MemoryStorePollingInterval = config.get("azureBlobCache.memoryStorePollingInterval", MemoryStorePollingInterval);
            MemoryStoreSlidingExpiration = config.get("azureBlobCache.memoryStoreSlidingExpiration", MemoryStoreSlidingExpiration);
            MemoryStoreAbsoluteExpiration = config.get("azureBlobCache.memoryStoreAbsoluteExpiration", MemoryStoreAbsoluteExpiration);

            ClientCacheMinutes = config.get("clientcache.minutes", ClientCacheMinutes);            
        }

        public virtual void Process(HttpContext current, IResponseArgs e)
        {
            throw new NotSupportedException("Only compatible with async pipeline, set 'ImageResizer.AsyncInterceptModule' instead of 'ImageResizer.InterceptModule' in Web.config");
        }

        public virtual bool CanProcess(HttpContext current, IResponseArgs e)
        {
            throw new NotSupportedException("Only compatible with async pipeline, set 'ImageResizer.AsyncInterceptModule' instead of 'ImageResizer.InterceptModule' in Web.config");
        }

        public virtual ICacheIndex GetConfiguredIndex()
        {
            if (_cacheProvider is AzureBlobCache azureCacheProvider)
            {
                return azureCacheProvider.GetIndex();
            }

            return null;
        }

        protected virtual void Start()
        {
            _cacheProvider = new AzureBlobCache(GetConnection(ConnectionName), ContainerName, GetCacheIndex(), GetCacheStore(), this);
            _keyGenerator = GetKeyGenerator();

            var index = GetConfiguredIndex();
            if (index is IStartable startable)
            {
                startable.Start();
            }

            _started = true;
        }

        protected virtual void Stop()
        {
            _started = false;

            var index = GetConfiguredIndex();
            if (index is IStoppable stoppable)
            {
                stoppable.Stop();
            }
        }

        protected virtual CancellationTokenSource GetTokenSource()
        {
            if (Debugger.IsAttached)
            {
                // Cache queries will timeout while stepping through source code
                // This keeps the requests active while debugging
                return new CancellationTokenSource();
            }
            else
            {
                return new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            }
        }

        protected virtual void RemapContentResponse(HttpContext context, IAsyncResponsePlan plan, byte[] contents)
        {
            context.RemapHandler(new MemoryCacheHandler(contents, plan.EstimatedContentType));
        }

        protected virtual void RemapMissResponse(HttpContext context, IAsyncResponsePlan plan)
        {
            context.RemapHandler(new NoCacheAsyncHandler(plan));
        }

        protected virtual void PreHandleImage(HttpContext context)
        {
            if (ClientCacheMinutes > 0)
                context.Response.Expires = ClientCacheMinutes;

            if (context.Request.IsAuthenticated)
                context.Response.CacheControl = HttpCacheability.Private.ToString();
            else
                context.Response.CacheControl = HttpCacheability.Public.ToString();

        }

        private ICacheKeyGenerator GetKeyGenerator()
        {
            return new MD5CacheKeyGenerator();
        }

        private string GetConnection(string name)
        {
            return ConfigurationManager.ConnectionStrings[name]?.ConnectionString;
        }

        private void ConfigureLogger(Config config)
        {
            var logManager = config.Plugins.LogManager;
            if (logManager == null)
            {
                config.Plugins.LoggingAvailable += CreateLogger;
            }
            else
            {
                CreateLogger(logManager);
            }
        }

        private void CreateLogger(ILogManager manager)
        {
            if (LoggingEnabled == false)
                return;

            if (_logger != null)
                return;

            _logger = manager.GetLogger(typeof(AzureBlobCachePlugin).Namespace);
        }

        private ICacheIndex GetCacheIndex()
        {
            var maxSizeMb = IndexMaxSizeMb > 0 ? IndexMaxSizeMb : (int?)null;
            var maxItems = IndexMaxItems > 0 ? IndexMaxItems : (int?)null;

            if (maxSizeMb.HasValue || maxItems.HasValue)
                return new AzureBlobCacheIndex(GetConnection(ConnectionName), ContainerName, maxSizeMb, maxItems, IndexPollingInterval, this);

            return new NullCacheIndex();
        }

        private ICacheStore GetCacheStore()
        {
            if (MemoryStoreLimitMb < 1)
                return new NullCacheStore();

            var absoluteExpiration = GetTimeSpan(MemoryStoreAbsoluteExpiration);
            var slidingExpiration = GetTimeSpan(MemoryStoreSlidingExpiration);
            var pollingInterval = GetTimeSpan(MemoryStorePollingInterval);

            return new AzureBlobCacheMemoryStore(MemoryStoreLimitMb, absoluteExpiration, slidingExpiration, pollingInterval, this);
        }

        private TimeSpan? GetTimeSpan(string value)
        {
            var result = (TimeSpan?)null;

            if (TimeSpan.TryParse(value, out var parsedValue))
                result = parsedValue;

            return result;
        }
    }
}
