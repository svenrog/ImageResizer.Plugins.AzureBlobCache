using Azure.Storage.Blobs;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    [TestClass]
    public class AzureBlobCacheIndexTests
    {
        private readonly Lazy<BlobContainerClient> _containerClient;
        private readonly Random _randomizer; 

        public AzureBlobCacheIndexTests()
        {
            _containerClient = new Lazy<BlobContainerClient>(InitializeContainer);
            _randomizer = new Random();            
        }

        [TestMethod]
        public async Task IndexWillAutoPruneByFileSize()
        {
            ClearIndex();

            var containerSize = await GetContainerSize();
            var containerSizeInMb = containerSize / 1_000_000.0;
            var uploadSize = 3_000_000;

            await InsertRandomIndexItems(uploadSize, 512_000, 10_000);

            var index = CreateIndexSizeConstrained((int)containerSizeInMb + 1);

            var initialCount = await GetContainerItems();

            var testKey = Guid.NewGuid();
            var testSize = 512_000;

            await index.NotifyAddedAsync(testKey, DateTime.UtcNow, testSize);

            var currentCount = await GetContainerItems();

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

            for (var i = 0; i < insertCount; i++)
            {
                var item = Guid.NewGuid();
                await index.NotifyAddedAsync(item, DateTime.UtcNow, 512_000);
                itemsAdded.Add(item);
            }
            
            var currentCount = await GetContainerItems();

            Assert.IsTrue(currentCount > 0);
            Assert.IsTrue(currentCount <= initialCount + insertCount);

            using (var context = new IndexContext())
            {
                foreach (var itemKey in itemsAdded)
                {
                    var existingEntity = context.IndexEntities.Find(itemKey);
                    Assert.IsNotNull(existingEntity);
                }
            }            
        }

        private AzureBlobCacheIndex CreateIndexSizeConstrained(int indexSizeConstraintMb)
        {
            return new AzureBlobCacheIndex(containerMaxSizeInMb: indexSizeConstraintMb, containerClientFactory: () => _containerClient.Value);
        }

        private AzureBlobCacheIndex CreateIndexItemConstrained(int indexItemConstraint)
        {
            return new AzureBlobCacheIndex(containerMaxItems: indexItemConstraint, containerClientFactory: () => _containerClient.Value);
        }

        private BlobContainerClient InitializeContainer()
        {
            var serviceClient = new BlobServiceClient(Config.BlobConnectionString);
            var container = serviceClient.GetBlobContainerClient(Constants.IndexTestContainerName);

            container.CreateIfNotExists();

            return container;
        }

        private async Task InsertRandomIndexItems(long totalSize, int maxSize, int minSize)
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

            using (var context = new IndexContext())
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

                await context.SaveChangesAsync();
            }
        }

        protected virtual void ClearIndex()
        {
            using (var context = new IndexContext())
            {
                context.IndexEntities.RemoveRange(context.IndexEntities);
                context.SaveChanges();
            }
        }

        protected virtual async Task<long> GetContainerSize()
        {
            try
            {
                using (var context = new IndexContext())
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
                using (var context = new IndexContext())
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
