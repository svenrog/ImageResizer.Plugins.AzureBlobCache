using System.Collections.Specialized;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Models
{
    public class AsyncTestPlan : IAsyncResponsePlan
    {
        public string EstimatedContentType { get; set; }
        public string EstimatedFileExtension { get; set; }
        public string RequestCachingKey { get; set; }

        public NameValueCollection RewrittenQuerystring { get; set; }
        public ReadStreamAsyncDelegate OpenSourceStreamAsync { get; set; }
        public WriteResultAsyncDelegate CreateAndWriteResultAsync { get; set; }
    }
}
