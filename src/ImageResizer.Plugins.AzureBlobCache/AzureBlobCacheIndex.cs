using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Caching.Core.Queueing;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache
{
    public class AzureBlobCacheIndex : ICacheIndex
    {
        private readonly string _connectionString;
        private readonly string _containerName;
        private readonly int _pageSize;
        private readonly long _containerMaxSize;
        private readonly SemaphoreSlim _synchronizer;
        private readonly Lazy<BlobContainerClient> _containerClient;
        private readonly IEqualityComparer<IndexItem> _itemComparer;

        protected readonly PriorityQueue<IndexItem> Index;

        private long _containerSize;
        private volatile bool _initialized;
        private volatile bool _initializing;
        
        public AzureBlobCacheIndex(string connectionString, string containerName, long containerMaxSizeInMb) : this(containerMaxSizeInMb)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _containerName = containerName ?? throw new ArgumentNullException(nameof(containerName));
        }

        public AzureBlobCacheIndex(long containerMaxSizeInMb, Func<BlobContainerClient> containerClientFactory = null)
        {
            _containerClient = new Lazy<BlobContainerClient>(containerClientFactory ?? InitializeContainer);
            _containerMaxSize  = containerMaxSizeInMb * 1_000_000;
            _containerSize = 0;

            _itemComparer = new IndexItemComparer();
            _synchronizer = new SemaphoreSlim(1);
            _pageSize = 1000;
            _initialized = false;

            Index = new PriorityQueue<IndexItem>();
        }

        public virtual async Task NotifyAddedAsync(Guid guid, DateTime modified, long size)
        {
            if (_containerMaxSize < 0)
                return;

            if (_initialized == false && _initializing == false)
            {
                _initializing = true;

                await PopulateIndex();
            }

            var item = new IndexItem(guid, modified, size);

            _containerSize += size;
            Index.Enqueue(item);

            if (_initialized)
            {
                await Prune();
            }
        }

        protected virtual async Task Prune()
        {
            await _synchronizer.WaitAsync();

            try
            {
                var removals = new List<IndexItem>();
                var removed = new List<IndexItem>();
                var containerAfterRemove = _containerSize;

                while (containerAfterRemove > _containerMaxSize)
                {
                    var item = Index.Dequeue();
                    removals.Add(item);
                    containerAfterRemove -= item.Size;
                }

                await removals.ForEachAsync(4, (i) => _containerClient.Value.DeleteBlobAsync($"{i.Key:D}"), (i, r) =>
                {
                    removed.Add(i);
                    _containerSize -= i.Size;
                });

                var unremoved = removals.Except(removed, _itemComparer);
                foreach (var item in unremoved)
                {
                    Index.Enqueue(item);
                }
            }
            finally
            {
                _synchronizer.Release();
            }            
        }

        protected virtual async Task PopulateIndex()
        {
            var enumerator = _containerClient.Value.GetBlobsAsync(BlobTraits.None, BlobStates.None)
                                                   .AsPages(default, _pageSize)
                                                   .GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    var blobPage = enumerator.Current;

                    foreach (var blob in blobPage.Values)
                    {
                        if (!blob.Properties.LastModified.HasValue)
                            continue;

                        if (!blob.Properties.ContentLength.HasValue)
                            continue;

                        var parsed = Guid.TryParseExact(blob.Name, "D", out Guid guid);
                        if (!parsed)
                            continue;

                        var dateTime = blob.Properties.LastModified.Value.UtcDateTime;
                        var size = blob.Properties.ContentLength.Value;

                        _containerSize += size;

                        var item = new IndexItem(guid, dateTime, size);

                        Index.Enqueue(item);
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            _initialized = true;
            _initializing = false;
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
