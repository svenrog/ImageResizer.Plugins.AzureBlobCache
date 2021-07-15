using ImageResizer.Plugins.AzureBlobCache.Tests.Models;
using ImageResizer.Plugins.AzureBlobCache.Tests.Testables;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Specialized;
using System.IO;
using System.Threading.Tasks;
using System.Web;

namespace ImageResizer.Plugins.AzureBlobCache.Tests
{
    [TestClass]
    public class AzureBlobCachePluginTests
    {
        [TestMethod]
        public async Task CanProcessAsync()
        {
            var plugin = CreateBlobCachePlugin();
            var localPath = "test/test4.jpg";

            using (var writer = new StringWriter())
            {
                var context = CreateHttpContext(localPath, writer);

                using (var stream = GetStream(localPath))
                {
                    var plan = CreatePlan(localPath, stream);

                    await plugin.ProcessAsync(context, plan);

                    Assert.AreEqual(200, context.Response.StatusCode);
                    Assert.AreEqual(plan.EstimatedContentType, context.Response.ContentType);
                    Assert.IsNotNull(context.Response.Output);
                }
            }            
        }

        [TestMethod]
        public void CantProcessSync()
        {
            var plugin = CreateBlobCachePlugin();
            var localPath = "test/test4.jpg";

            using (var writer = new StringWriter())
            {
                var context = CreateHttpContext(localPath, writer);

                using (var stream = GetStream(localPath))
                {
                    var args = CreateArgs(localPath, stream);

                    Assert.ThrowsException<NotSupportedException>(() => plugin.Process(context, args));
                }
            }
        }

        private TestResponseArgs CreateArgs(string path, Stream stream)
        {
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(fileName);
            var mimeMapping = MimeMapping.GetMimeMapping(fileName);

            return new TestResponseArgs
            {
                RequestKey = path,
                SuggestedExtension = extension?.TrimStart('.'),
                ResizeImageToStream =  (s) => stream.CopyTo(s),
                RewrittenQuerystring = new NameValueCollection(0),
            };
        }

        private AsyncTestPlan CreatePlan(string path, Stream stream)
        {
            var fileName = Path.GetFileName(path);
            var extension = Path.GetExtension(fileName);
            var mimeMapping = MimeMapping.GetMimeMapping(fileName);
            
            return new AsyncTestPlan
            {
                RequestCachingKey = path,
                EstimatedContentType = mimeMapping,
                EstimatedFileExtension = extension?.TrimStart('.'),
                OpenSourceStreamAsync = () => Task.FromResult(stream),
                CreateAndWriteResultAsync = async (s, p) => await stream.CopyToAsync(s),
                RewrittenQuerystring = new NameValueCollection(0),
            };
        }

        private Stream GetStream(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        }

        private HttpContext CreateHttpContext(string localPath, TextWriter writer)
        {
            return CreateHttpContext(localPath, string.Empty, writer);
        }

        private HttpContext CreateHttpContext(string localPath, string queryString, TextWriter writer)
        {
            return new HttpContext
            (
                new HttpRequest(string.Empty, "https://test.com/" + localPath, queryString),
                new HttpResponse(writer)
            );
        }

        private AzureBlobCachePlugin CreateBlobCachePlugin()
        {
            if (string.IsNullOrEmpty(Config.BlobConnectionString))
                Assert.Inconclusive("Test requires a connection named 'ResizerAzureBlobs' with a connection string to an Azure storage account.");

            var plugin = new TestableAzureBlobCachePlugin();

            plugin.ManualStart();

            return plugin;
        }
    }
}
