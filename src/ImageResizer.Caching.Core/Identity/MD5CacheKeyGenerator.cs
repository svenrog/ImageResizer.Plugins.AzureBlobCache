using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ImageResizer.Caching.Core.Identity
{
    public class MD5CacheKeyGenerator : ICacheKeyGenerator
    {
        private readonly Encoding _encoding;
        private readonly IFormatProvider _formatProvider;

        public MD5CacheKeyGenerator()
        {
            _encoding = Encoding.Default;
            _formatProvider = NumberFormatInfo.InvariantInfo;
        }

        public Guid Generate(string path)
        {
            var bytes = _encoding.GetBytes(path);
            return new Guid(MD5.Create().ComputeHash(bytes));
        }

        public Guid Generate(string path, string extension)
        {
            return Generate(path + extension);
        }

        public Guid Generate(string path, DateTime modified)
        {
            var append = modified.Ticks.ToString(_formatProvider);
            return Generate(path + append);
        }

        public Guid Generate(string path, string extension, DateTime modified)
        {
            return Generate(path + extension, modified);
        }
    }
}
