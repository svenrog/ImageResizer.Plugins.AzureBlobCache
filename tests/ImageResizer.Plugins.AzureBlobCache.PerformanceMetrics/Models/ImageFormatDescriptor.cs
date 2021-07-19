namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class ImageFormatDescriptor
    {
        public ResizeOptions Options { get; set; }
        public int AbsoluteCount { get; set; }
        public double Ratio { get; set; }
    }
}
