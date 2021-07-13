using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheIndex : ICacheIndex
    {
        private readonly string _efConnectionName;
        private readonly string _connectionString;
        private readonly string _containerName;

        private readonly int _pruneSize;
        private readonly int _pruneInterval = 10;
        private readonly long _containerMaxSize;
        private readonly CleaningStrategy _cleanupStrategy;
        private readonly Lazy<CloudBlobContainer> _containerClient;

        private volatile int _pruneCounter = 0;

        public AzureBlobCacheIndex(string connectionString, string containerName, int? containerMaxSizeInMb = null, int? containerMaxItems = null, string efConnectionName = null) : this(containerMaxSizeInMb, containerMaxItems, efConnectionName: efConnectionName)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));    
        }

        public AzureBlobCacheIndex(int? containerMaxSizeInMb = null, int? containerMaxItems = null, Func<CloudBlobContainer> containerClientFactory = null, string efConnectionName = null)
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

            _efConnectionName = efConnectionName;
            _containerClient = new Lazy<CloudBlobContainer>(containerClientFactory ?? InitializeContainer);
            _pruneSize = 100;
            _pruneInterval = 50;
            _pruneCounter = 0;
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
                    var pruned = false;

                    if (_pruneCounter++ % _pruneInterval == 0)
                        pruned = await Prune();

                    if (!pruned)
                        await context.SaveChangesDatabaseWinsAsync();
                }
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
            catch (InvalidOperationException)
            {
                return 0;
            }            
        }

        protected virtual async Task<bool> Prune()
        {
            var removals = new List<IndexEntity>();
            var removed = new List<IndexEntity>();
            var containerSize = await GetContainerSize();

            if (containerSize <= _containerMaxSize)
                return false;

            using (var context = GetContext())
            {
                var candidates = await context.IndexEntities.OrderBy(x => x.Modified)
                                                            .Take(_pruneSize)
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

        private Task<bool> DeleteBlob(Guid key)
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
