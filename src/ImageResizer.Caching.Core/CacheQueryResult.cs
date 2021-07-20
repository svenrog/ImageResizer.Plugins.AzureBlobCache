using System;

namespace ImageResizer.Caching.Core
{
    [Flags]
    public enum CacheQueryResult
    {
        /// <summary>
        /// Failed to acquire a lock on the cached item within the timeout period.
        /// </summary>
        Failed = 0,
        /// <summary>
        /// The item wasn't cached, but was successfully added to the cache.
        /// </summary>
        Miss = 1,
        /// <summary>
        /// The item was already in the cache.
        /// </summary>
        Hit = 2,
        /// <summary>
        /// The item fetch caused the request to fail (used when testing).
        /// </summary>
        Error = 4
    }
}
