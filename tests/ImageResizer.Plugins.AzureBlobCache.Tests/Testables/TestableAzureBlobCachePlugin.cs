using ImageResizer.Plugins.AzureBlobCache.Handlers;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Testables
{
    public class TestableAzureBlobCachePlugin : AzureBlobCachePlugin
    {
        public void ManualStart()
        {
            Start();
        }

        protected override void RemapHitResponse(HttpContext context, IAsyncResponsePlan plan, byte[] data)
        {
            var rewriter = new ResponseTransformer(data, plan.EstimatedContentType);

            try
            {
                rewriter.TransformResponse(context.Response)
                        .GetAwaiter()
                        .GetResult();
            }
            catch (HttpException ex) when (ex.Message.StartsWith("OutputStream"))
            {
                return;
            }
        }

        public override void LoadSettings(Configuration.Config config)
        {
            base.LoadSettings(config);

            ConnectionName = Constants.BlobConnectionName;
            ContainerName = Constants.CacheTestContainerName;
        }
    }
}
