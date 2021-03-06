using ImageResizer.Caching.Core;
using ImageResizer.Caching.Core.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    [TestClass]
    public class AzureBlobCacheTests
    {
        [TestMethod]
        public async Task CanCreate()
        {
            var blobCache = CreateDefaultBlobCache();
            
            var result = await CreateAsync(blobCache, "resources/test1.jpg");

            Assert.AreEqual(CacheQueryResult.Miss, result.Result);
            Assert.IsNotNull(result.Contents);
            Assert.IsTrue(result.Contents.Length > 0);
        }

        [TestMethod]
        public async Task CanGet()
        {
            var blobCache = CreateDefaultBlobCache();
            var path = "resources/test1.jpg";

            await CreateAsync(blobCache, path);

            var result = await GetAsync(blobCache, path);

            Assert.AreEqual(CacheQueryResult.Hit, result.Result);
            Assert.IsNotNull(result.Contents);
            Assert.IsTrue(result.Contents.Length > 0);
        }

        [TestMethod]
        public async Task CanMiss()
        {
            var blobCache = CreateDefaultBlobCache();
            var path = "resources/neverbeforerequested.jpg";

            var result = await GetAsync(blobCache, path);

            Assert.AreEqual(CacheQueryResult.Miss, result.Result);
            Assert.IsNull(result.Contents);
        }

        [TestMethod]
        public async Task CanGetWithIndexConfigured()
        {
            var blobCache = CreateIndexedBlobCache();
            var path = "resources/test4.jpg";

            await CreateAsync(blobCache, path);

            var result = await GetAsync(blobCache, path);

            Assert.AreEqual(CacheQueryResult.Hit, result.Result);
            Assert.IsNotNull(result.Contents);
            Assert.IsTrue(result.Contents.Length > 0);
        }

        [TestMethod]
        public async Task CanGetWithStoreConfigured()
        {
            var blobCache = CreateMemoryStoreBlobCache();
            var path = "resources/test5.jpg";

            await CreateAsync(blobCache, path);

            var result = await GetAsync(blobCache, path);

            Assert.AreEqual(CacheQueryResult.Hit, result.Result);
            Assert.IsNotNull(result.Contents);
            Assert.IsTrue(result.Contents.Length > 0);
        }

        [TestMethod]
        public async Task CanGetParallel()
        {
            var blobCache = CreateDefaultBlobCache();
            var path = "resources/test1.jpg";

            await CreateAsync(blobCache, path);

            var tasks = new List<Task<ICacheResult>>(10);

            for (var i = 0; i < 10; i++)
            {
                tasks.Add(GetAsync(blobCache, path));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                Assert.AreEqual(CacheQueryResult.Hit, result.Result);
                Assert.IsNotNull(result.Contents);
                Assert.IsTrue(result.Contents.Length > 0);
            }
        }

        [TestMethod]
        public async Task CanCreateParallel()
        {
            var blobCache = CreateDefaultBlobCache();
            var path = "resources/test2.png";

            var tasks = new List<Task<ICacheResult>>(10);

            for (var i = 0; i < 10; i++)
            {
                tasks.Add(CreateAsync(blobCache, path));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var result in results)
            {
                Assert.AreEqual(CacheQueryResult.Miss, result.Result);
                Assert.IsNotNull(result.Contents);
                Assert.IsTrue(result.Contents.Length > 0);
            }
        }

        private Task<ICacheResult> GetAsync(AzureBlobCache cache, string path)
        {
            return GetAsync(cache, path, default(CancellationToken));
        }

        private Task<ICacheResult> GetAsync(AzureBlobCache cache, string path, CancellationToken token)
        {
            var extension = Path.GetExtension(path);
            var generator = CreateCacheKeyGenerator();

            return cache.GetAsync(generator.Generate(path, extension), token);
        }

        private Task<ICacheResult> CreateAsync(AzureBlobCache cache, string path)
        {
            return CreateAsync(cache, path, default(CancellationToken));
        }

        private async Task<ICacheResult> CreateAsync(AzureBlobCache cache, string path, CancellationToken token)
        {
            var generator = CreateCacheKeyGenerator();
            var extension = Path.GetExtension(path);
            var bufferSize = 4096;

            using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, true))
            {
                return await cache.CreateAsync(generator.Generate(path, extension), token, (stream) => fileStream.CopyToAsync(stream, bufferSize));
            }
        }

        private ICacheKeyGenerator CreateCacheKeyGenerator()
        {
            return new MD5CacheKeyGenerator();
        }

        private AzureBlobCache CreateDefaultBlobCache()
        {
            if (string.IsNullOrEmpty(Config.BlobConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerAzureBlobs' with a connection string to an Azure storage account.");

            return new AzureBlobCache(Config.BlobConnectionString, Constants.CacheTestContainerName);
        }

        private AzureBlobCache CreateIndexedBlobCache()
        {
            if (string.IsNullOrEmpty(Config.BlobConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerAzureBlobs' with a connection string to an Azure storage account.");

            if (string.IsNullOrEmpty(Config.DbContextConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerEFConnection' with a connection string to an Azure storage account.");

            var index = new AzureBlobCacheIndex(Config.BlobConnectionString, Constants.CacheTestContainerName, 10);
            return new AzureBlobCache(Config.BlobConnectionString, Constants.CacheTestContainerName, index, new NullCacheStore());
        }

        private AzureBlobCache CreateMemoryStoreBlobCache()
        {
            if (string.IsNullOrEmpty(Config.BlobConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerAzureBlobs' with a connection string to an Azure storage account.");

            var store = new AzureBlobCacheMemoryStore(200, TimeSpan.FromMinutes(1));
            return new AzureBlobCache(Config.BlobConnectionString, Constants.CacheTestContainerName, store);
        }
    }
}
