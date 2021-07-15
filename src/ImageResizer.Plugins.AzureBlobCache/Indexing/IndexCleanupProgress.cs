using ImageResizer.Caching.Core.Indexing;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexCleanupProgress : IRebuildProgress
    {
        public CleanupPhase CleanupPhase { get; set; }

        public int ItemsInIndex { get; set; }
        public int DiscoveredBlobs { get; set; }
        public int AddedIndexItems { get; set; }
        public int RemovedIndexItems { get; set; }
        public int Errors { get; set; }
    }
}
