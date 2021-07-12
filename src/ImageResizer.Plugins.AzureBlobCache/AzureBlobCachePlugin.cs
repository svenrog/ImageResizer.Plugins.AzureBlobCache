using ImageResizer.Caching;
using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Exceptions;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Configuration;
using ImageResizer.Plugins.AzureBlobCache.Handlers;
using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCachePlugin : IAsyncTyrantCache, ICache, IPlugin
    {
        private volatile bool _started = false;

        protected string ConnectionName = "ResizerAzureBlobs";
        protected string ContainerName = "imagecache";
        protected int TimeoutSeconds = 5;

        protected int MemoryStoreLimitMb;
        protected string MemoryStorePollingInterval = "00:04:01";

        protected string IndexConnectionName = "ResizerEFConnection";
        protected int IndexMaxSizeMb;
        protected int IndexMaxItems;

        private IAsyncCacheProvider _cacheProvider;
        private CancellationTokenSource _tokenSource;

        public async Task ProcessAsync(HttpContext context, IAsyncResponsePlan plan)
        {
            var cacheResult = await _cacheProvider.GetAsync(plan.RequestCachingKey, plan.EstimatedFileExtension, _tokenSource.Token);
            if (cacheResult.Result == CacheQueryResult.Failed)
            {
                throw new CacheTimeoutException($"Failed to aquire lock on file '{plan.RequestCachingKey}' in {TimeoutSeconds} seconds.");
            }

            if (cacheResult.Result == CacheQueryResult.Miss)
            {
                var path = plan.RequestCachingKey;
                var extension = plan.EstimatedFileExtension;
                var token = _tokenSource.Token;

                cacheResult = await _cacheProvider.CreateAsync(path, extension, token, (stream) => plan.CreateAndWriteResultAsync(stream, plan));
            }

            if (cacheResult.Result == CacheQueryResult.Failed)
            {
                throw new CacheTimeoutException($"Failed to aquire lock on file '{plan.RequestCachingKey}' in {TimeoutSeconds} seconds.");
            }

            if (cacheResult.Result == CacheQueryResult.Hit)
            {
                await RemapResponseAsync(context, cacheResult.Contents, plan.EstimatedContentType);
            }
        }

        public bool CanProcess(HttpContext context, IAsyncResponsePlan plan)
        {
            var instructions = new Instructions(plan.RewrittenQuerystring);
            if (instructions.Cache == ServerCacheMode.No) return false;

            return _started;
        }

        public IPlugin Install(Config config)
        {
            LoadSettings(config);
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
        /// Uses the defaults from the resizing.diskcache section in the specified configuration.
        /// Throws an invalid operation exception if the DiskCache is already started.
        /// </summary>
        public void LoadSettings(Config config)
        {
            ConnectionName = config.get("azureBlobCache.connectionName", ConnectionName);
            ContainerName = config.get("azureBlobCache.containerName", ContainerName);
            TimeoutSeconds = config.get("azureBlobCache.timeoutSeconds", TimeoutSeconds);
            IndexConnectionName = config.get("azureBlobCache.indexConnectionName", IndexConnectionName);
            IndexMaxSizeMb = config.get("azureBlobCache.indexMaxSizeMb", IndexMaxSizeMb);
            IndexMaxItems = config.get("azureBlobCache.indexMaxItems", IndexMaxItems);
            MemoryStoreLimitMb = config.get("azureBlobCache.memoryStoreLimitMb", MemoryStoreLimitMb);
            MemoryStorePollingInterval = config.get("azureBlobCache.memoryStorePollingInterval", MemoryStorePollingInterval);   
        }

        public void Process(HttpContext current, IResponseArgs e)
        {
            throw new NotSupportedException("Only compatible with async pipeline, set 'ImageResizer.AsyncInterceptModule' instead of 'ImageResizer.InterceptModule' in Web.config");
        }

        public bool CanProcess(HttpContext current, IResponseArgs e)
        {
            throw new NotSupportedException("Only compatible with async pipeline, set 'ImageResizer.AsyncInterceptModule' instead of 'ImageResizer.InterceptModule' in Web.config");
        }

        protected virtual Task RemapResponseAsync(HttpContext context, byte[] data, string contentType)
        {
            context.RemapHandler(new MemoryCacheHandler(data, contentType));
            return Task.CompletedTask;
        }

        protected virtual void Start()
        {
            _tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
            _cacheProvider = new AzureBlobCache(GetConnectionString(), ContainerName, GetCacheIndex(), GetCacheStore());
            _started = true;
        }

        protected virtual void Stop()
        {
            _started = false;
        }

        private string GetConnectionString()
        {
            return ConfigurationManager.ConnectionStrings[ConnectionName]?.ConnectionString;
        }

        private ICacheIndex GetCacheIndex()
        {
            if (IndexMaxSizeMb > 0)
                return new AzureBlobCacheIndex(IndexMaxSizeMb, IndexMaxItems, efConnectionName: GetConnectionString());

            return new NullCacheIndex();
        }

        private ICacheStore GetCacheStore()
        {
            if (MemoryStoreLimitMb > 0)
                return new AzureBlobCacheMemoryStore(MemoryStoreLimitMb, MemoryStorePollingInterval, null);
            
            return new NullCacheStore();
        }
    }
}
