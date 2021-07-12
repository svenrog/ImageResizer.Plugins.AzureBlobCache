using System.Configuration;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    public static class Config
    {
        public static readonly string BlobConnectionString = ConfigurationManager.ConnectionStrings["ResizerAzureBlobs"]?.ConnectionString;
        public static readonly string DbContextConnectionString = ConfigurationManager.ConnectionStrings["ResizerEFConnection"]?.ConnectionString;
    }
}
