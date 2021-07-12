using System;

namespace ImageResizer.Caching.Core
{
    public interface ICacheResult
    {
        CacheQueryResult Result { get; }
        byte[] Contents { get; }
    }
}
