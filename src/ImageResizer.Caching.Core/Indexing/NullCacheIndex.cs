using System;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core.Indexing
{
    public class NullCacheIndex : ICacheIndex
    {
        public Task NotifyAddedAsync(Guid guid, DateTime modified, long size)
        {
            return Task.CompletedTask;
        }
    }
}
