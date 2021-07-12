using System;
using System.IO;

namespace ImageResizer.Plugins.AzureBlobCache.Tests.Helpers
{
    public static class DataHelper
    {
        private static readonly Random _randomizer;

        static DataHelper()
        {
            _randomizer = new Random();
        }

        public static byte[] GetByteArray(int size)
        {
            var bytes = new byte[size];
            _randomizer.NextBytes(bytes);
            return bytes;
        }

        public static Stream GetStream(int size)
        {
            return new MemoryStream(GetByteArray(size));
        }
    }
}
