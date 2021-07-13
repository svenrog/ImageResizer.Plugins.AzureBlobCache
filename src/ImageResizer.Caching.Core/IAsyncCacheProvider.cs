using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageResizer.Caching.Core
{
    public interface IAsyncCacheProvider
    {
        Task<ICacheResult> GetAsync(string path, string extension, CancellationToken cancellationToken);
        Task<ICacheResult> CreateAsync(string path, string extension, CancellationToken cancellationToken, Func<Stream, Task> asyncWriter);
    }
}