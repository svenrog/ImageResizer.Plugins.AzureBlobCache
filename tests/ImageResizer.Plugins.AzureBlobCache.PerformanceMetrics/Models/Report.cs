using System.Collections.Generic;
using System.Linq;

namespace ImageResizer.Plugins.AzureBlobCache.PerformanceMetrics.Models
{
    public class Report
    {
        public IList<DataPoint> DataPoints { get; set; }

        public double Average => DataPoints?.Average(x => x.ElapsedMilliseconds) ?? 0;
    }
}
