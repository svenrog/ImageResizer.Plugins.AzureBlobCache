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

        [TestMethod]
        public void HandlesSlidingExpiry()
        {
            // Internal update time for memory cache is 1 second.
            // For this test to work expiration must be above that.

            var testExpiry = TimeSpan.FromSeconds(1).Add(TimeSpan.FromMilliseconds(100));
            var testPoll = TimeSpan.FromMilliseconds(250);

            var store = CreateStore(slidingExpiry: testExpiry);
            var key = Guid.NewGuid();
            var testSize = 50_000;
            var cacheResult = CreateResult(testSize);

            store.Insert(key, cacheResult);

            ICacheResult stored;

            for (var i = 0; i < 6; i++)
            {
                Thread.Sleep(testPoll);

                stored = store.Get(key);

                Assert.IsNotNull(stored);
                Assert.IsNotNull(stored.Contents);
                Assert.AreEqual(stored.Contents.Length, testSize);
            }

            Thread.Sleep(testExpiry + testPoll);

            stored = store.Get(key);

            Assert.IsNull(stored);
        }

        [TestMethod]
        public void HandlesAbsoluteExpiry()
        {
            var testExpiry = TimeSpan.FromSeconds(2);
            var testPoll = TimeSpan.FromMilliseconds(500);

            var store = CreateStore(absoluteExpiry: testExpiry);
            var key = Guid.NewGuid();
            var testSize = 50_000;
            var cacheResult = CreateResult(testSize);

            store.Insert(key, cacheResult);

            Thread.Sleep(testPoll);

            var stored = store.Get(key);

            Assert.IsNotNull(stored);
            Assert.IsNotNull(stored.Contents);
            Assert.AreEqual(stored.Contents.Length, testSize);

            Thread.Sleep(testExpiry);

            stored = store.Get(key);

            Assert.IsNull(stored);
        }

        private ICacheResult CreateResult(int size = 50_000)
        {
            return new TestCacheResult
            {
                Result = CacheQueryResult.Hit,
                Contents = DataHelper.GetByteArray(size)
            };
        }

        private TestableAzureBlobCacheMemoryStore CreateStore(TimeSpan? absoluteExpiry = null, TimeSpan? slidingExpiry = null, TimeSpan? pollingInterval = null, int sizeLimitMb = 1)
        {
            if (!absoluteExpiry.HasValue && !slidingExpiry.HasValue)
                absoluteExpiry = TimeSpan.FromMinutes(1);

            return new TestableAzureBlobCacheMemoryStore(sizeLimitMb, absoluteExpiry, slidingExpiry, pollingInterval);
        }
    }
}
