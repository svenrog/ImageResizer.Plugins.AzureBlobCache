using System;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core.Indexing
{
    public interface ICacheIndex
    {
        Task NotifyAddedAsync(Guid guid, DateTime modified, long size);
    }
}
