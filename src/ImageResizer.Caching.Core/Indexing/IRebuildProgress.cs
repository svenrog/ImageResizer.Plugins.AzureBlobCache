namespace ImageResizer.Caching.Core.Indexing
{
    public interface IRebuildProgress
    {
        CleanupPhase CleanupPhase { get; set; }

        int ItemsInIndex { get; set; }
        int DiscoveredBlobs { get; set; }
        int AddedIndexItems { get; set; }
        int RemovedIndexItems { get; set; }
        int Errors { get; set; }
    }
}
