using System;

namespace ImageResizer.Caching.Core.Identity
{
    public interface ICacheKeyGenerator
    {
        Guid Generate(string path);
        Guid Generate(string path, string extension);
        Guid Generate(string path, DateTime modified);
        Guid Generate(string path, string extension, DateTime modified);
    }
}
