using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Caching.Core.Indexing;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using ImageResizer.Plugins.AzureBlobCache.Tests.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    [TestClass]
    public class AzureBlobCacheIndexTests
    {
        private readonly Lazy<CloudBlobContainer> _containerClient;
        private readonly Random _randomizer; 

        public AzureBlobCacheIndexTests()
        {
            _containerClient = new Lazy<CloudBlobContainer>(InitializeContainer);
            _randomizer = new Random();            
        }

        [TestMethod]
        public async Task IndexWillAutoPruneByFileSize()
        {
            ClearIndex();

            var uploadSize = 15_000_000;

            await InsertRandomIndexItems(uploadSize, 512_000, 10_000);

            var index = CreateIndexSizeConstrained(1);
            
            index.Start();

            var initialCount = await GetContainerItems();

            var testKey = Guid.NewGuid();
            var testSize = 512_000;

            await index.NotifyAddedAsync(testKey, DateTime.UtcNow, testSize);
            Thread.Sleep(3000);

            var currentCount = await GetContainerItems();

            index.Stop();

            Assert.IsTrue(currentCount > 0);
            Assert.IsTrue(initialCount >= currentCount);
        }

        [TestMethod]
        public async Task IndexWillAutoPruneByItems()
        {
            ClearIndex();

            await InsertRandomIndexItems(15_000_000, 512_000, 50_000);

            var initialCount = await GetContainerItems();
            var index = CreateIndexItemConstrained(initialCount);
            var insertCount = 10;
            var itemsAdded = new List<Guid>();

            index.Start();

            for (var i = 0; i < insertCount; i++)
            {
                var item = Guid.NewGuid();
                await index.NotifyAddedAsync(item, DateTime.UtcNow, 512_000);
                itemsAdded.Add(item);
            }

            Thread.Sleep(3000);

            var currentCount = await GetContainerItems();

            index.Stop();

            Assert.IsTrue(currentCount > 0);
            Assert.IsTrue(currentCount < initialCount + insertCount);

            using (var context = CreateEfContext())
            {
                foreach (var itemKey in itemsAdded)
                {
                    var existingEntity = context.IndexEntities.Find(itemKey);
                    Assert.IsNotNull(existingEntity);
                }
            }
        }

        [TestMethod]
        public async Task IndexCanRebuild()
        {
            ClearIndex();

            await UploadRandomBlobs(3_000_000, 512_000, 50_000);
            await InsertRandomIndexItems(3_000_000, 512_000, 50_000);
            
            var initialCount = await GetContainerItems();
            var index = CreateIndexItemConstrained(100_000);
            var progress = (IRebuildProgress)null;

            await index.RebuildAsync(progressCallback: (p) => { progress = p; });
            
            Assert.IsNotNull(progress);
            Assert.AreEqual(progress.CleanupPhase, CleanupPhase.Completed);
            Assert.AreEqual(progress.Errors, 0);
            Assert.IsTrue(progress.RemovedIndexItems > 0);
            Assert.IsTrue(progress.DiscoveredBlobs > 0);
        }

        [TestMethod]
        public async Task IndexRebuildCanBeCancelled()
        {
            ClearIndex();

            await InsertRandomIndexItems(3_000_000, 512_000, 50_000);

            var initialCount = await GetContainerItems();
            var index = CreateIndexItemConstrained(100_000);

            var tokenSource = new CancellationTokenSource(200);

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(() => index.RebuildAsync(tokenSource.Token, (p) => { }));
        }

        private IndexContext CreateEfContext()
        {
            if (string.IsNullOrEmpty(Config.DbContextConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerEFConnection' with a connection string to a SQL database.");

            return new IndexContext();
        }

        private AzureBlobCacheIndex CreateIndexSizeConstrained(int indexSizeConstraintMb)
        {
            return new AzureBlobCacheIndex(indexSizeConstraintMb, null, "00:00:00.1", () => _containerClient.Value);
        }

        private AzureBlobCacheIndex CreateIndexItemConstrained(int indexItemConstraint)
        {
            return new AzureBlobCacheIndex(null, indexItemConstraint, "00:00:00.1", () => _containerClient.Value);
        }

        private CloudBlobContainer InitializeContainer()
        {
            if (!CloudStorageAccount.TryParse(Config.BlobConnectionString, out CloudStorageAccount storageAccount))
                Assert.Inconclusive("Test requires a connection named 'ResizerAzureBlobs' with a connection string to an Azure storage account.");

            var serviceClient = storageAccount.CreateCloudBlobClient();
            var container = serviceClient.GetContainerReference(Constants.IndexTestContainerName);

            container.CreateIfNotExists();

            return container;
        }

        private async Task InsertRandomIndexItems(long totalSize, int maxSize, int minSize)
        {
            var sizes = GetRandomSizes(totalSize, maxSize, minSize);

            using (var context = CreateEfContext())
            {
                context.Configuration.AutoDetectChangesEnabled = false;

                foreach (var size in sizes)
                {
                    var key = Guid.NewGuid();
                    var minuteOffset = _randomizer.Next(30, 100);
                    var modified = DateTime.UtcNow.AddMinutes(-minuteOffset);
                    var entity = new IndexEntity(key, modified, size);

                    context.IndexEntities.Add(entity);
                }

                await context.SaveChangesDatabaseWinsAsync();
            }
        }

        private async Task UploadRandomBlobs(long totalSize, int maxSize, int minSize)
        {
            var sizes = GetRandomSizes(totalSize, maxSize, minSize);
            var uploaded = new List<long>(sizes.Count);

            await sizes.ForEachAsync(4, async (size) =>
            {
                var key = Guid.NewGuid();

                using (var stream = DataHelper.GetStream(size))
                {
                    var blob = _containerClient.Value.GetBlockBlobReference($"{key:D}");
                    await blob.UploadFromStreamAsync(stream);
                }
            });
        }

        private IList<int> GetRandomSizes(long totalSize, int maxSize, int minSize)
        {
            var sizeRemaining = totalSize;
            var sizes = new List<int>();

            while (sizeRemaining > 0)
            {
                var max = sizeRemaining > int.MaxValue ? maxSize : (int)Math.Min(maxSize, sizeRemaining);
                var min = Math.Min(minSize, max);
                var size = _randomizer.Next(min, max);

                sizes.Add(size);
                sizeRemaining -= size;
            }

            return sizes;
        }

        protected virtual void ClearIndex()
        {
            using (var context = CreateEfContext())
            {
                context.IndexEntities.RemoveRange(context.IndexEntities);
                context.SaveChangesDatabaseWins();
            }
        }

        protected virtual async Task<long> GetContainerSize()
        {
            try
            {
                using (var context = CreateEfContext())
                {
                    return await context.IndexEntities.SumAsync(x => x.Size);
                }
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }

        protected virtual async Task<int> GetContainerItems()
        {
            try
            {
                using (var context = CreateEfContext())
                {
                    return await context.IndexEntities.CountAsync();
                }
            }
            catch (InvalidOperationException)
            {
                return 0;
            }
        }
    }
}
