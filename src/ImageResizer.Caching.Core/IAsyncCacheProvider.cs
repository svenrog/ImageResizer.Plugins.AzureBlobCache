using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core
{
    public interface IAsyncCacheProvider
    {
        Task<ICacheResult> GetAsync(Guid key, CancellationToken cancellationToken);
        Task<ICacheResult> CreateAsync(Guid key, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter);
    }
}