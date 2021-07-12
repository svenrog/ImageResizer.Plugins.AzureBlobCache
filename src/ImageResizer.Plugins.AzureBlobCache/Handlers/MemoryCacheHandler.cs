using ImageResizer.Caching.Core.Handlers;
using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache.Handlers
{
    public class MemoryCacheHandler : AsyncHttpHandlerBase
    {
        private readonly ResponseTransformer _transformer;

        public MemoryCacheHandler(byte[] data, string contentType)
        {
            _transformer = new ResponseTransformer(data, contentType);
        }

        public override Task ProcessRequestAsync(HttpContext context)
        {
            return _transformer.TransformResponse(context.Response);
        }
    }
}
