using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class ImageInstructionGroup
    {
        public IEnumerable<string> Images { get; set; }
        public Instructions Instructions { get; set; }
    }
}
