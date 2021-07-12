using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ImageResizer.Caching.Core.Extensions;
using ImageResizer.Plugins.AzureBlobCache.Tests.Helpers;
using ImageResizer.Plugins.AzureBlobCache.Tests.Testables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
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
        public async Task IndexWillAutoPrune()
        {
            //await UploadRandomBlobs(1_000_000_000, 512_000, 40_000);

            var containerSize = await SizeOfContainerAsync();
            var containerSizeInMb = containerSize / 1_000_000.0;
            var uploadSize = 3_000_000;

            await UploadRandomBlobs(uploadSize, 512_000, 10_000);

            var index = CreateIndex((int)containerSizeInMb + 1);
            await index.ManuallyPopulateIndex();

            var initialCount = index.InternalIndex.Count;

            var testKey = Guid.NewGuid();
            var testSize = 512_000;

            await UploadTestBlob(testKey, testSize);
            await index.NotifyAddedAsync(testKey, DateTime.UtcNow, testSize);

            var currentCount = index.InternalIndex.Count;

            Assert.IsTrue(currentCount > 0);
            Assert.IsTrue(initialCount >= currentCount);
        }

        private TestableAzureBlobCacheIndex CreateIndex(int indexSizeConstraintMb)
        {
            return new TestableAzureBlobCacheIndex(indexSizeConstraintMb, () => _containerClient.Value);
        }

        private BlobContainerClient InitializeContainer()
        {
            var serviceClient = new BlobServiceClient(Config.ConnectionString);
            var container = serviceClient.GetBlobContainerClient(Constants.IndexTestContainerName);

            container.CreateIfNotExists();

            return container;
        }

        private async Task UploadTestBlob(Guid key, int size)
        {
            var bytes = DataHelper.GetByteArray(size);
            await _containerClient.Value.UploadBlobAsync($"{key:D}", new MemoryStream(bytes));
        }

        private async Task UploadRandomBlobs(long totalSize, int maxSize, int minSize)
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

            var uploaded = new List<long>(sizes.Count);

            await sizes.ForEachAsync(4, async (size) => 
            {
                var key = Guid.NewGuid();

                using (var stream = DataHelper.GetStream(size))
                {
                    await _containerClient.Value.UploadBlobAsync($"{key:D}", stream);
                }
            });
        }


        private async Task<long> SizeOfContainerAsync()
        {
            var totalSize = 0L;
            var enumerator = _containerClient.Value.GetBlobsAsync(BlobTraits.None, BlobStates.None)
                                                   .AsPages(default, 1000)
                                                   .GetAsyncEnumerator();

            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    var blobPage = enumerator.Current;

                    foreach (var blob in blobPage.Values)
                    {
                        if (!blob.Properties.ContentLength.HasValue)
                            continue;
                        
                        var size = blob.Properties.ContentLength.Value;

                        totalSize += size;
                    }
                }
            }
            finally
            {
                await enumerator.DisposeAsync();
            }

            return totalSize;
        }
    }
}
