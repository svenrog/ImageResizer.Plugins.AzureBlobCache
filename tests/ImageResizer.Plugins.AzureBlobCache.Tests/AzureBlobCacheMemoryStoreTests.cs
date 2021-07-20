using ImageResizer.Caching.Core;
using ImageResizer.Plugins.AzureBlobCache.Tests.Helpers;
using ImageResizer.Plugins.AzureBlobCache.Tests.Models;
using ImageResizer.Plugins.AzureBlobCache.Tests.Testables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    [TestClass]
    public class AzureBlobCacheMemoryStoreTests
    {
        [TestMethod]
        public void CanStore()
        {
            var store = CreateStore();
            var key = Guid.NewGuid();
            var testSize = 50_000;
            var cacheResult = CreateResult(testSize);

            store.Insert(key, cacheResult);

            var stored = store.Get(key);

            Assert.IsNotNull(stored);
            Assert.IsNotNull(stored.Contents);
            Assert.AreEqual(stored.Contents.Length, testSize);
        }

        private ICacheResult CreateResult(int size = 50_000)
        {
            return new TestCacheResult
            {
                Result = CacheQueryResult.Hit,
                Contents = DataHelper.GetByteArray(size)
            };
        }

        private TestableAzureBlobCacheMemoryStore CreateStore(int sizeLimitMb = 1, TimeSpan? pollingInterval = null)
        {
            return new TestableAzureBlobCacheMemoryStore(sizeLimitMb, TimeSpan.FromMinutes(1), pollingInterval);
        }
    }
}
