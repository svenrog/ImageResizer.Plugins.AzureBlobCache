using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Threading;
using ImageResizer.Configuration.Logging;
using ImageResizer.Plugins.AzureBlobCache.Extensions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
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
        private readonly ICacheIndex _cacheIndex;
        private readonly ICacheStore _cacheStore;
        private readonly ILoggerProvider _log;
        private readonly bool _storeDisabled;

        private readonly Lazy<CloudBlobContainer> _containerClient;

        public AzureBlobCache(string connectionString, string containerName) : this(connectionString, containerName, new NullCacheIndex(), new NullCacheStore(), null) { }
        public AzureBlobCache(string connectionString, string containerName, ILoggerProvider loggerProvider) : this(connectionString, containerName, new NullCacheIndex(), new NullCacheStore(), loggerProvider) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheStore cacheStore) : this(connectionString, containerName, new NullCacheIndex(), cacheStore, null) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheStore cacheStore, ILoggerProvider loggerProvider) : this(connectionString, containerName, new NullCacheIndex(), cacheStore, loggerProvider) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore) : this(connectionString, containerName, cacheIndex, cacheStore, null) { }
        public AzureBlobCache(string connectionString, string containerName, ICacheIndex cacheIndex, ICacheStore cacheStore, ILoggerProvider loggerProvider) : this(cacheIndex, cacheStore, loggerProvider)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }

        public AzureBlobCache(Func<CloudBlobContainer> containerClientFactory) : this(new NullCacheIndex(), new NullCacheStore(), containerClientFactory: containerClientFactory) { }
        public AzureBlobCache(ILoggerProvider loggerProvider, Func<CloudBlobContainer> containerClientFactory) : this(new NullCacheIndex(), new NullCacheStore(), loggerProvider, containerClientFactory) { }
        public AzureBlobCache(ICacheIndex cacheIndex, ILoggerProvider loggerProvider, Func<CloudBlobContainer> containerClientFactory) : this(cacheIndex, new NullCacheStore(), loggerProvider, containerClientFactory) { }
        protected AzureBlobCache(ICacheIndex cacheIndex, ICacheStore cacheStore, ILoggerProvider loggerProvider = null, Func<CloudBlobContainer> containerClientFactory = null)
        {            
            _cacheIndex = cacheIndex ?? throw new ArgumentNullException(nameof(cacheIndex));
            _cacheStore = cacheStore ?? throw new ArgumentNullException(nameof(cacheStore));

            _synchronizer = new SemaphoreSynchronizer<Guid>();
            _containerClient = new Lazy<CloudBlobContainer>(containerClientFactory ?? InitializeContainer);
            _log = loggerProvider;

            _storeDisabled = cacheStore is NullCacheStore;
        }

        public async Task<ICacheResult> GetAsync(Guid key, CancellationToken cancellationToken)
        {
            // Optimistic cache fetch
            var storedResult = _cacheStore.Get(key);
            if (storedResult != null)
            {
                if (_log.IsDebugEnabled())
                    _log.Debug($"Cache request '{key:D}' delivered from optimistic memory cache query.");

                return storedResult;
            }               

            var blob = GetBlob(key);
            var readLock = _synchronizer[key];

            try
            {
                await readLock.WaitAsync(cancellationToken);

                if (_storeDisabled)
                    TryRelaseSemaphore(readLock);

                // Pessimistic cache fetch
                storedResult = _cacheStore.Get(key);
                if (storedResult != null)
                {
                    if (_log.IsDebugEnabled())
                        _log.Debug($"Cache request '{key:D}' delivered from pessimistic memory cache query.");

                    return storedResult;
                }

                try
                {
                    using (var stream = new MemoryStream(4096))
                    {
                        await blob.DownloadToStreamAsync(stream, cancellationToken)
                                  .ConfigureAwait(false);

                        var result = CreateResult(CacheQueryResult.Hit, stream);

                        _cacheStore.Insert(key, result);

                        return result;
                    }                    
                }
                catch (StorageException ex) when (ex.Message.Contains("(404)"))
                {
                    if (_log.IsDebugEnabled())
                        _log.Debug($"Cache request '{key:D}' not found in blob storage.");

                    return CreateResult(CacheQueryResult.Miss);
                }
            }
            catch (OperationCanceledException)
            {
                if (_log.IsWarnEnabled())
                    _log.Warn($"Cache request '{key:D}' timed out.");

                return CreateResult(CacheQueryResult.Failed);
            }
            finally
            {
                if (!_storeDisabled)
                    TryRelaseSemaphore(readLock);

                _synchronizer.TryRemove(key);
            }
        }

        public async Task<ICacheResult> CreateAsync(Guid key, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter)
        {
            var blob = GetBlob(key);
            var writeLock = _synchronizer[key];

            try
            {
                await writeLock.WaitAsync(cancellationToken);

                using (var stream = new MemoryStream(4096))
                {
                    await asyncWriter(stream);

                    try
                    {
                        stream.Seek(0, SeekOrigin.Begin);

                        await blob.UploadFromStreamAsync(stream, cancellationToken)
                                  .ConfigureAwait(false);

                        var result = CreateResult(CacheQueryResult.Miss, stream);

                        _cacheStore.Insert(key, RewriteResult(CacheQueryResult.Hit, result.Contents));

                        await _cacheIndex.NotifyAddedAsync(key, DateTime.UtcNow, result.Contents.Length)
                                         .ConfigureAwait(false);

                        return result;
                    }
                    catch (OperationCanceledException)
                    {
                        // Operation was cancelled while uploading or modifying index
                        // In this case, the image has been resized and should be returned
                        // Not returning it will cause another resize

                        return CreateResult(CacheQueryResult.Failed, stream);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled while waiting for another operation
                // In this case, the image has not been resized yet

                return CreateResult(CacheQueryResult.Failed);
            }
            finally
            {
                TryRelaseSemaphore(writeLock);
                _synchronizer.TryRemove(key);
            }
        }

        private bool TryRelaseSemaphore(SemaphoreSlim semaphore)
        {
            if (semaphore.CurrentCount > 1)
                return false;

            try
            {
                semaphore.Release();
                return true;
            }
            catch (SemaphoreFullException)
            {
                // Under really heavy load, multiple threads will be in here in the same context.
                // One might be just one step ahead of the other, trying to release the semaphore.
                return false;
            }
            catch (ObjectDisposedException)
            {
                // Under really heavy load, multiple threads will be trying to remove unused semaphores.
                // This may lead to a thread lagging a bit behind trying to release an already disposed semaphore.
                return false;
            }
        }

        public ICacheIndex GetIndex()
        {
            return _cacheIndex;
        }

        public ICacheStore GetStore()
        {
            return _cacheStore;
        }

        private ICacheResult RewriteResult(CacheQueryResult result, byte[] contents)
        {
            return new AzureBlobCacheQueryResult
            {
                Result = result,
                Contents = contents
            };
        }

        private ICacheResult CreateResult(CacheQueryResult result, MemoryStream stream = null)
        {
            stream?.Seek(0, SeekOrigin.Begin);

            return new AzureBlobCacheQueryResult
            {
                Result = result,
                Contents = stream?.ToArray()
            };
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
