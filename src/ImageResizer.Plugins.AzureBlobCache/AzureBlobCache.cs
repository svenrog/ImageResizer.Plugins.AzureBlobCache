using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Identity;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCache : IAsyncCacheProvider
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        private readonly IThreadSynchronizer<Guid, SemaphoreSlim> _synchronizer;
        private readonly ICacheKeyGenerator _keyGenerator;
        private readonly ICacheIndex _cacheIndex;
        private readonly ICacheStore _cacheStore;

        private readonly Lazy<BlobContainerClient> _containerClient;

        public AzureBlobCache(string connectionString, string containerName) : this(connectionString, containerName, new NullCacheIndex(), new NullCacheStore(), new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheStore cacheStore) : this(connectionString, containerName, new NullCacheIndex(), cacheStore, new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore) : this(connectionString, containerName, cacheIndex, cacheStore, new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore, ICacheKeyGenerator keyGenerator) : this(cacheIndex, cacheStore, keyGenerator)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }

        public AzureBlobCache(Func<BlobContainerClient> containerClientFactory) : this(new NullCacheIndex(), new NullCacheStore(), new MD5CacheKeyGenerator(), containerClientFactory) { }
        public AzureBlobCache(ICacheIndex cacheIndex, Func<BlobContainerClient> containerClientFactory) : this(cacheIndex, new NullCacheStore(), new MD5CacheKeyGenerator(), containerClientFactory) { }
        public AzureBlobCache(ICacheIndex cacheIndex, ICacheStore cacheStore, Func<BlobContainerClient> containerClientFactory) : this(cacheIndex, cacheStore, new MD5CacheKeyGenerator(), containerClientFactory) { }
        protected AzureBlobCache(ICacheIndex cacheIndex, ICacheStore cacheStore, ICacheKeyGenerator keyGenerator, Func<BlobContainerClient> containerClientFactory = null)
        {            
            _cacheIndex = cacheIndex ?? throw new ArgumentNullException(nameof(cacheIndex));
            _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
            _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));

            _synchronizer = new SemaphoreSynchronizer<Guid>();
            _containerClient = new Lazy<BlobContainerClient>(containerClientFactory ?? InitializeContainer);
        }

        public Task<ICacheResult> GetAsync(string path, string extension, CancellationToken cancellationToken)
        {
            return GetAsync(path, extension, null, cancellationToken);
        }

        public Task<ICacheResult> GetAsync(string path, string extension, DateTime modified, CancellationToken cancellationToken)
        {
            return GetAsync(path, extension, modified, cancellationToken);
        }

        public Task<ICacheResult> CreateAsync(string path, string extension, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter)
        {
            return CreateAsync(path, extension, null, cancellationToken, asyncWriter);
        }

        public Task<ICacheResult> CreateAsync(string path, string extension, DateTime modified, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter)
        {
            return CreateAsync(path, extension, modified, cancellationToken, asyncWriter);
        }

        private async Task<ICacheResult> GetAsync(string path, string extension, DateTime? modified, CancellationToken cancellationToken)
        {
            var key = GetKey(path, extension, modified);

            // Optimistic cache fetch
            var storedResult = _cacheStore.Get(key);
            if (storedResult != null)
                return storedResult;

            var blob = GetBlob(key);
            var readLock = _synchronizer[key];

            try
            {
                await readLock.WaitAsync(cancellationToken);
                readLock.Release();

                // Pessimistic cache fetch
                storedResult = _cacheStore.Get(key);
                if (storedResult != null)
                    return storedResult;

                try
                {
                    var stream = new MemoryStream();

                    await blob.DownloadToAsync(stream, cancellationToken)
                              .ConfigureAwait(false);

                    return CreateResult(CacheQueryResult.Hit, stream);
                }
                catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
                {
                    return CreateResult(CacheQueryResult.Miss);
                }
                finally
                {
                    _synchronizer.TryRemove(key);
                }
            }
            catch (OperationCanceledException)
            {
                return CreateResult(CacheQueryResult.Failed);
            }
        }       

        private async Task<ICacheResult> CreateAsync(string path, string extension, DateTime? modified, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter)
        {
            var key = GetKey(path, extension, modified);
            var blob = GetBlob(key);
            var writeLock = _synchronizer[key];

            try
            {
                await writeLock.WaitAsync(cancellationToken);

                try
                {
                    using (var stream = new MemoryStream(4096))
                    {
                        await asyncWriter(stream);

                        stream.Seek(0, SeekOrigin.Begin);

                        await blob.UploadAsync(stream, true, cancellationToken)
                                  .ConfigureAwait(false);

                        stream.Seek(0, SeekOrigin.Begin);

                        var result = CreateResult(CacheQueryResult.Hit, stream);

                        #pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        _cacheIndex.NotifyAddedAsync(key, modified ?? DateTime.UtcNow, result.Contents.Length)
                                   .ConfigureAwait(false);
                        #pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                        _cacheStore.Insert(key, result);

                        return result;
                    }
                }
                finally
                {
                    writeLock.Release();
                    _synchronizer.TryRemove(key);
                }
            }
            catch (OperationCanceledException)
            {
                return CreateResult(CacheQueryResult.Failed);
            }
        }

        private ICacheResult CreateResult(CacheQueryResult result, MemoryStream stream = null)
        {
            return new AzureBlobCacheQueryResult
            {
                Result = result,
                Contents = stream?.ToArray()
            };
        }

        private Guid GetKey(string path, string extension, DateTime? modified = null)
        {
            return modified.HasValue ? _keyGenerator.Generate(path, extension, modified.Value) : _keyGenerator.Generate(path, extension);
        }

        private BlobClient GetBlob(Guid key)
        {
            return _containerClient.Value.GetBlobClient($"{key:D}");
        }

        private BlobContainerClient InitializeContainer()
        {
            var serviceClient = new BlobServiceClient(_connectionString);
            var container = serviceClient.GetBlobContainerClient(_containerName);

            container.CreateIfNotExists();

            return container;
        }
    }
}
