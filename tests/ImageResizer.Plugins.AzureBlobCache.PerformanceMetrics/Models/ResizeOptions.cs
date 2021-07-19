using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class ResizeOptions
    {
        public int? Width { get; set; }
        public int? Height { get; set; }

        public string Format { get; set; }
        public FitMode Mode { get; set; } = FitMode.None;
        public int Quality { get; set; } = 95;
    }
}
