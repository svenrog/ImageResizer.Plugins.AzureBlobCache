using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Identity;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Threading;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.IO;
using System.Net;
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

        private readonly Lazy<CloudBlobContainer> _containerClient;

        public AzureBlobCache(string connectionString, string containerName) : this(connectionString, containerName, new NullCacheIndex(), new NullCacheStore(), new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheStore cacheStore) : this(connectionString, containerName, new NullCacheIndex(), cacheStore, new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore) : this(connectionString, containerName, cacheIndex, cacheStore, new MD5CacheKeyGenerator()) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore, ICacheKeyGenerator keyGenerator) : this(cacheIndex, cacheStore, keyGenerator)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }

        public AzureBlobCache(Func<CloudBlobContainer> containerClientFactory) : this(new NullCacheIndex(), new NullCacheStore(), new MD5CacheKeyGenerator(), containerClientFactory) { }
        public AzureBlobCache(ICacheIndex cacheIndex, Func<CloudBlobContainer> containerClientFactory) : this(cacheIndex, new NullCacheStore(), new MD5CacheKeyGenerator(), containerClientFactory) { }
        public AzureBlobCache(ICacheIndex cacheIndex, ICacheStore cacheStore, Func<CloudBlobContainer> containerClientFactory) : this(cacheIndex, cacheStore, new MD5CacheKeyGenerator(), containerClientFactory) { }
        protected AzureBlobCache(ICacheIndex cacheIndex, ICacheStore cacheStore, ICacheKeyGenerator keyGenerator, Func<CloudBlobContainer> containerClientFactory = null)
        {            
            _cacheIndex = cacheIndex ?? throw new ArgumentNullException(nameof(cacheIndex));
            _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));
            _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));

            _synchronizer = new SemaphoreSynchronizer<Guid>();
            _containerClient = new Lazy<CloudBlobContainer>(containerClientFactory ?? InitializeContainer);
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

                    await blob.DownloadToStreamAsync(stream, cancellationToken)
                              .ConfigureAwait(false);

                    return CreateResult(CacheQueryResult.Hit, stream);
                }
                catch (StorageException ex) when (ex.Message.Contains("(404)"))
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

                        await blob.UploadFromStreamAsync(stream, cancellationToken)
                                  .ConfigureAwait(false);

                        stream.Seek(0, SeekOrigin.Begin);

                        var result = CreateResult(CacheQueryResult.Hit, stream);

                        _cacheStore.Insert(key, result);

                        await _cacheIndex.NotifyAddedAsync(key, modified ?? DateTime.UtcNow, result.Contents.Length)
                                         .ConfigureAwait(false);

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

        private CloudBlockBlob GetBlob(Guid key)
        {
            return _containerClient.Value.GetBlockBlobReference($"{key:D}");
        }

        private CloudBlobContainer InitializeContainer()
        {
            if (!CloudStorageAccount.TryParse(_connectionString, out CloudStorageAccount storageAccount))
                throw new ArgumentException("connectionString could not be parsed");

            var serviceClient = storageAccount.CreateCloudBlobClient();
            var container = serviceClient.GetContainerReference(_containerName);

            container.CreateIfNotExists();

            return container;
        }
    }
}
