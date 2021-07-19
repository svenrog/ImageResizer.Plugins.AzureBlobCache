using ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models;
using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Pages
{
    public class CategoryPage : PageBase
    {
        private static readonly List<ImageFormatDescriptor> _imageFormats = new List<ImageFormatDescriptor>
        {
            new ImageFormatDescriptor { Ratio = 0.25, Options = new ResizeOptions { Width = 393, Height = 294, Mode = FitMode.Crop, Quality = 75, Format = "jpg" } },
            new ImageFormatDescriptor { Ratio = 0.75, Options = new ResizeOptions { Width = 62, Height = 62, Mode = FitMode.Crop, Quality = 75, Format = "jpg" } },
        };

        public CategoryPage(ICollection<string> images) : base(images, _imageFormats)
        {

        }
    }
}
