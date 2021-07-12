using Azure.Storage.Blobs;
using ImageResizer.Caching.Core.Queueing;
using ImageResizer.Plugins.AzureBlobCache.Indexing;
using System;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCacheIndex : AzureBlobCacheIndex
    {
        public TestableAzureBlobCacheIndex(long containerMaxSizeInMb, Func<BlobContainerClient> containerClientFactory = null) : base(containerMaxSizeInMb, containerClientFactory) { }

        public virtual PriorityQueue<IndexItem> InternalIndex => Index;

        public virtual Task ManuallyPopulateIndex()
        {
            return PopulateIndex();
        }
    }
}
