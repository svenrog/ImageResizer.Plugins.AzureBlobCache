using ImageResizer.Caching;
using System.Collections.Specialized;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Models
{
    public class TestResponseArgs : IResponseArgs
    {
        public string RequestKey { get; set; }
        public NameValueCollection RewrittenQuerystring { get; set; }
        public string SuggestedExtension { get; set; }
        public IResponseHeaders ResponseHeaders { get; set; }
        public ModifiedDateDelegate GetModifiedDateUTC { get; set; }
        public bool HasModifiedDate { get; set; }
        public ResizeImageDelegate ResizeImageToStream { get; set; }
    }
}
