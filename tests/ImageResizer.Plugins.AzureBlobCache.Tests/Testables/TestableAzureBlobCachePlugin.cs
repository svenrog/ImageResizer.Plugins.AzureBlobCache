using ImageResizer.Plugins.AzureBlobCache.Handlers;
using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCachePlugin : AzureBlobCachePlugin
    {
        public TestableAzureBlobCachePlugin(string connectionString, string containerName = "imagecache", int timeoutSeconds = 10)
        {
            ConnectionString = connectionString;
            ContainerName = containerName;
            TimeoutSeconds = timeoutSeconds;
        }

        public void ManualStart()
        {
            Start();
        }

        protected override async Task RemapResponseAsync(HttpContext context, byte[] data, string contentType)
        {
            var rewriter = new ResponseTransformer(data, contentType);

            try
            {
                await rewriter.TransformResponse(context.Response);
            }
            catch (HttpException ex) when (ex.Message.StartsWith("OutputStream is not available"))
            {
                return;
            }
        }
    }
}
