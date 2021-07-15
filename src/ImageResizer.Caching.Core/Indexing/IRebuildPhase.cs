using System;

namespace ImageResizer.Caching.Core.Indexing
{
    [Flags]
    public enum CleanupPhase : byte
    {
        NotStarted = 0,
        Cancelled = 1,
        Discovery = 2,
        Synchronization = 4,
        Completed = 8,
    }
}
