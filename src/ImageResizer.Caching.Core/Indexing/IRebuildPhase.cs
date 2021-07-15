namespace ImageResizer.Caching.Core.Indexing
{
    public enum CleanupPhase : byte
    {
        NotStarted = 0,
        Discovery = 1,
        Synchronization = 2,
        Completed = 3,
    }
}
