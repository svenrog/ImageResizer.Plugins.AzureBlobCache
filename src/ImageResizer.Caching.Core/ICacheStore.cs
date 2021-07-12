using System;

namespace ImageResizer.Caching.Core
{
    public interface ICacheStore
    {
        ICacheResult Get(Guid key);
        void Insert(Guid key, ICacheResult value);
    }
}
