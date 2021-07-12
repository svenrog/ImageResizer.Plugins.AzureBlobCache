using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache.Handlers
{
    public class ResponseTransformer
    {
        private readonly byte[] _data;
        private readonly string _contentType;

        public ResponseTransformer(byte[] data, string contentType)
        {
            _data = data;
            _contentType = contentType;
        }

        public virtual Task TransformResponse(HttpResponse response)
        {
            response.StatusCode = 200;
            response.BufferOutput = true;
            response.ContentType = _contentType;

            return response.OutputStream.WriteAsync(_data, 0, _data.Length);
        }
    }
}
