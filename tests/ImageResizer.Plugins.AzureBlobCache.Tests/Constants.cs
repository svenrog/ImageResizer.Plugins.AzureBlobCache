using System.Configuration;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    public static class Constants
    {
        public const string BlobConnectionName = "ResizerAzureBlobs";

        public const string CacheTestContainerName = "blobtestcache";
        public const string IndexTestContainerName = "indextestcache";
    }
}
