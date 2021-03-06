using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Operation;
using ImageResizer.Configuration.Logging;
using ImageResizer.Plugins.AzureBlobCache.Extensions;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheIndex : IRebuildableCacheIndex, IStartable, IStoppable, IDisposable
    {
        private readonly string _connectionString;
        private readonly string _containerName;

        private readonly long _containerMaxSize;
        private readonly CleaningStrategy _cleanupStrategy;
        private readonly Lazy<CloudBlobContainer> _containerClient;
        private readonly IndexWorker _indexWorker;
        private readonly ILoggerProvider _log;

        private bool _disposed;

        public AzureBlobCacheIndex(string connectionString, string containerName, int? containerMaxSizeInMb = null, int? containerMaxItems = null, string workerPollingInterval = null, ILoggerProvider loggerProvider = null) : this(containerMaxSizeInMb, containerMaxItems, workerPollingInterval, loggerProvider)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));    
        }

        public AzureBlobCacheIndex(int? containerMaxSizeInMb = null, int? containerMaxItems = null, string workerPollingInterval = null, ILoggerProvider loggerProvider = null, Func<CloudBlobContainer> containerClientFactory = null)
        {
            if (containerMaxSizeInMb.HasValue)
            {
                _cleanupStrategy = CleaningStrategy.Size;
                _containerMaxSize = containerMaxSizeInMb.Value * 1_000_000;
            }
            else if (containerMaxItems.HasValue)
            {
                _cleanupStrategy = CleaningStrategy.Items;
                _containerMaxSize = containerMaxItems.Value;
            }
            else
            {
                throw new ArgumentNullException($"Both {nameof(containerMaxSizeInMb)} and {nameof(containerMaxItems)} cannot be null.");
            }

            TimeSpan.TryParse(workerPollingInterval ?? "00:05:00", out var workerInterval);

            _log = loggerProvider;
            _indexWorker = new IndexWorker(() => TrimIndex(100), workerInterval, 25);
            _containerClient = new Lazy<CloudBlobContainer>(containerClientFactory ?? InitializeContainer);
        }

        public virtual async Task NotifyAddedAsync(Guid guid, DateTime modified, long size)
        {
            if (_containerMaxSize < 0)
                return;

            using (var context = GetContext())
            {
                var updatedIndex = false;
                var existingEntity = await context.IndexEntities.FindAsync(guid)
                                                                .ConfigureAwait(false);

                if (existingEntity == null)
                {
                    var newEntity = new IndexEntity(guid, modified, size);
                    context.IndexEntities.Add(newEntity);
                    updatedIndex = true;
                }
                else if (existingEntity.Size != size)
                {
                    existingEntity.Size = size;
                    existingEntity.Modified = modified; 
                    updatedIndex = true;
                }

                if (updatedIndex)
                {
                    await context.SaveChangesDatabaseWinsAsync();

                    if (_log.IsDebugEnabled())
                    {
                        if (existingEntity == null)
                            _log.Debug($"Added item with key '{guid:D}' to index.");
                        else
                            _log.Debug($"Updated item with key '{guid:D}' in index.");
                    }                        

                    _indexWorker.Poll();
                }
            }
        }

        public Task RebuildAsync(Action<IRebuildProgress> progressCallback)
        {
            return RebuildAsync(default, progressCallback);
        }

        public async Task RebuildAsync(CancellationToken cancellationToken, Action<IRebuildProgress> progressCallback)
        {
            var indexItems = await GetIndexCount();
            var storageEntities = await DiscoverAsync(indexItems, progressCallback, cancellationToken);

            await Synchronize(indexItems, storageEntities, progressCallback, cancellationToken);
        }

        public virtual void Start()
        {
            _indexWorker.Start();
        }

        public virtual void Stop()
        {
            _indexWorker.Stop();
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _indexWorker.Dispose();
                }

                _disposed = true;
            }
        }

        protected virtual IndexContext GetContext()
        {
            return new IndexContext();
        }

        protected virtual async Task<long> GetContainerSize()
        {
            try 
            {
                using (var context = GetContext())
                {
                    if (_cleanupStrategy == CleaningStrategy.Size)
                        return await context.IndexEntities.SumAsync(x => x.Size);
                    
                    if (_cleanupStrategy == CleaningStrategy.Items)
                        return await context.IndexEntities.CountAsync();

                    throw new InvalidOperationException("Could not find implementation for strategy");
                }
            }
            catch (InvalidOperationException ex)
            {
                if (_log.IsErrorEnabled())
                    _log.Error("Error occurred estimating container size", ex);

                return 0;
            }
        }

        protected virtual async Task<int> GetIndexCount()
        {
            try
            {
                using (var context = GetContext())
                {
                    return await context.IndexEntities.CountAsync();
                }
            }
            catch (InvalidOperationException ex)
            {
                if (_log.IsErrorEnabled())
                    _log.Error("Error occurred counting items in index", ex);

                return 0;
            }
        }

        protected virtual async Task<bool> TrimIndex(int maxItems)
        {
            var removals = new List<IndexEntity>();
            var removed = new List<IndexEntity>();
            var containerSize = await GetContainerSize();

            if (containerSize <= _containerMaxSize)
                return false;

            using (var context = GetContext())
            {
                var candidates = await context.IndexEntities.OrderBy(x => x.Modified)
                                                            .Take(maxItems)
                                                            .ToListAsync();

                foreach (var candidate in candidates)
                {
                    if (containerSize <= _containerMaxSize)
                        break;

                    removals.Add(candidate);

                    if (_cleanupStrategy == CleaningStrategy.Size)
                    {
                        containerSize -= candidate.Size;
                    }
                    else if (_cleanupStrategy == CleaningStrategy.Items)
                    {
                        containerSize--;
                    }
                    else
                    {
                        if (_log.IsErrorEnabled())
                            _log.Error($"Not implemented cleaning strategy encountered '{_cleanupStrategy}'");

                        throw new InvalidOperationException("Could not find implementation for strategy");
                    }
                }

                await removals.ForEachAsync(4, (entity) => DeleteBlob(entity.Key), (entity, result) =>
                {
                    removed.Add(entity);
                });

                context.IndexEntities.RemoveRange(removals);

                await context.SaveChangesDatabaseWinsAsync();

                return true;
            }
        }
        
        protected virtual async Task<IList<IndexEntity>> DiscoverAsync(int indexItemCount, Action<IRebuildProgress> progressCallback, CancellationToken cancellationToken)
        {
            var progress = new IndexCleanupProgress
            {
                CleanupPhase = CleanupPhase.Discovery,
                ItemsInIndex = indexItemCount
            };

            progressCallback(progress);

            var entities = new List<IndexEntity>(indexItemCount);

            BlobContinuationToken token = null;

            do
            {
                var segment = await _containerClient.Value.ListBlobsSegmentedAsync(token, cancellationToken);

                foreach (var blob in segment.Results.OfType<CloudBlockBlob>())
                {
                    if (!blob.Properties.LastModified.HasValue)
                        continue;

                    var parsed = Guid.TryParseExact(blob.Name, "D", out Guid guid);
                    if (!parsed)
                        continue;

                    var dateTime = blob.Properties.LastModified.Value.UtcDateTime;
                    var size = blob.Properties.Length;

                    var item = new IndexEntity(guid, dateTime, size);

                    entities.Add(item);

                    progress.DiscoveredBlobs += 1;
                    progressCallback(progress);
                }

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException("Cancellation requested");

                token = segment.ContinuationToken;
            }
            while (token != null);
            return entities;
        }

        protected virtual async Task Synchronize(int indexItemCount, IList<IndexEntity> existingEntities, Action<IRebuildProgress> progressCallback, CancellationToken cancellationToken)
        {
            var comparer = new IndexEntityComparer();
            var progress = new IndexCleanupProgress
            {
                CleanupPhase = CleanupPhase.Synchronization,
                ItemsInIndex = indexItemCount,
                DiscoveredBlobs = existingEntities.Count,
            };

            progressCallback(progress);

            IList<IndexEntity> itemsInIndex;

            using (var context = GetContext())
            {
                itemsInIndex = await context.IndexEntities.ToListAsync();
                var itemsToRemoveFromIndex = itemsInIndex.Except(existingEntities, comparer);

                try
                {
                    context.IndexEntities.RemoveRange(itemsToRemoveFromIndex);
                    progress.RemovedIndexItems = await context.SaveChangesDatabaseWinsAsync(cancellationToken);
                }
                catch (DataException)
                {
                    progress.Errors++;
                }
            }

            var itemsToAddToIndex = existingEntities.Except(itemsInIndex, comparer);

            foreach (var item in itemsToAddToIndex)
            {
                try
                {
                    await NotifyAddedAsync(item.Key, item.Modified, item.Size);
                    progress.AddedIndexItems++;
                }
                catch (DataException)
                {
                    progress.Errors++;
                }
                
                progressCallback(progress);

                if (cancellationToken.IsCancellationRequested)
                    throw new TaskCanceledException("Cancellation requested");
            }

            progress.CleanupPhase = CleanupPhase.Completed;
            progressCallback(progress);
        }

        protected virtual Task<bool> DeleteBlob(Guid key)
        {
            var reference = _containerClient.Value.GetBlockBlobReference($"{key:D}");
            return reference.DeleteIfExistsAsync();
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
