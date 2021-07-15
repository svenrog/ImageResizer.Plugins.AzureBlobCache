using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core.Indexing
{
    public interface IRebuildableCacheIndex : ICacheIndex
    {
        Task RebuildAsync(Action<IRebuildProgress> progressCallback);
        Task RebuildAsync(CancellationToken cancellationToken, Action<IRebuildProgress> progressCallback);
    }
}
