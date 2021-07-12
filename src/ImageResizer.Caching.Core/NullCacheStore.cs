using System;

namespace ImageResizer.Caching.Core
{
    public class NullCacheStore : ICacheStore
    {
        public ICacheResult Get(Guid key)
        {
            return null;
        }

        public void Insert(Guid key, ICacheResult value)
        {
            // Do nothing
        }
    }
}
