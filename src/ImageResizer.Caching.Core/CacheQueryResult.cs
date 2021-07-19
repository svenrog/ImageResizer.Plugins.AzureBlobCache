using System;

namespace ImageResizer.Caching.Core
{
    [Flags]
    public enum CacheQueryResult
    {
        /// <summary>
        /// Failed to acquire a lock on the cached item within the timeout period
        /// </summary>
        Failed = 0,
        /// <summary>
        /// The item wasn't cached, but was successfully added to the cache (or queued, in which case you should read .Data instead of .PhysicalPath)
        /// </summary>
        Miss = 1,
        /// <summary>
        /// The item was already in the cache.
        /// </summary>
        Hit = 2,
        /// <summary>
        /// The item fetch caused a fatal error.
        /// </summary>
        Fatal = 4
    }
}
