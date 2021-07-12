using System.Configuration;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    public static class Config
    {
        public static readonly string ConnectionString = ConfigurationManager.AppSettings["AzureBlobConnectionString"];
    }
}
