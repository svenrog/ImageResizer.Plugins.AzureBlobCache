using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Pages
{
    public class ProductPage : PageBase
    {
        private static readonly List<ImageFormatDescriptor> _imageFormats = new List<ImageFormatDescriptor>
        {
            new ImageFormatDescriptor { AbsoluteCount = 3, Options = new ResizeOptions { Width = 750, Height = 750, Quality = 75, Format = "jpg" } },
            new ImageFormatDescriptor { AbsoluteCount = 3, Options = new ResizeOptions { Width = 68, Height = 68, Quality = 75, Format = "jpg" } },
            new ImageFormatDescriptor { AbsoluteCount = 1, Options = new ResizeOptions { Width = 52, Height = 52, Mode = FitMode.Pad, Quality = 75, Format = "jpg" } },
            new ImageFormatDescriptor { Ratio = 1, Options = new ResizeOptions { Width = 250, Height = 180, Mode = FitMode.Pad, Format = "jpg" } },
        };

        public ProductPage(ICollection<string> images) : base(images, _imageFormats)
        {

        }
    }
}
