using System.Configuration;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    public static class Constants
    {
        public const string CacheTestContainerName = "blobtestcache";
        public const string IndexTestContainerName = "indextestcache";
        public const string WriterContextKey = "imagecachewriter";

        public static readonly IPlugin[] DefaultPlugins = new IPlugin[]
        {
            new AzureBlobCachePlugin()
        };
    }
}
