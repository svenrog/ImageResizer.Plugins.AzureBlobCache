using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Pages;
using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.TestPages
{
    public class CampaignPage : PageBase
    {
        private static readonly List<ImageFormatDescriptor> _imageFormats = new List<ImageFormatDescriptor>
        {
            new ImageFormatDescriptor { AbsoluteCount = 4, Options = new ResizeOptions { Width = 1370, Quality = 80, Format = "jpg" } },
            new ImageFormatDescriptor { Ratio = 1, Options = new ResizeOptions { Width = 393, Height = 294, Mode = FitMode.Crop, Quality = 75, Format = "jpg" } },
        };

        public CampaignPage(ICollection<string> images) : base(images, _imageFormats)
        {

        }
    }
}
