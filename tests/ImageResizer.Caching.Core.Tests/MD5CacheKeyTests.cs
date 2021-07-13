using ImageResizer.Caching.Core.Identity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace ImageResizer.Caching.Core.Tests
{
    [TestClass]
    public class MD5CacheKeyTests
    {
        [TestMethod]
        public void CanGenerate()
        {
            var generator = new MD5CacheKeyGenerator();

            var firstPath = "made/up/path/to/somefile.jpg";
            var secondPath = "made/up/path/to/someotherfile.jpg";

            var firstKey = generator.Generate(firstPath);
            var secondKey = generator.Generate(secondPath);
            var secondGeneration = generator.Generate(firstPath);

            Assert.AreNotEqual(firstKey, secondKey);
            Assert.AreEqual(firstKey, secondGeneration);
        }

        [TestMethod]
        public void HandlesModifiedDate()
        {
            var generator = new MD5CacheKeyGenerator();

            var path = "made/up/path/to/somefile.jpg";
            var firstTime = DateTime.UtcNow;
            var secondTime = firstTime.AddSeconds(1);

            var firstKey = generator.Generate(path, firstTime);
            var secondKey = generator.Generate(path, secondTime);

            Assert.AreNotEqual(firstKey, secondKey);
        }

        [TestMethod]
        public void IsFairlyDistinct()
        {
            var generator = new MD5CacheKeyGenerator();
            var iterations = 1_000_000;

            var paths = new HashSet<string>();
            var results = new HashSet<Guid>();

            for (var i = 0; i < iterations; i++)
            {
                var path = $"{Guid.NewGuid()}.jpg";

                if (paths.Contains(path))
                    continue;

                paths.Add(path);
            }

            foreach (var path in paths)
            {
                var generated = generator.Generate(path);

                Assert.IsFalse(results.Contains(generated));

                results.Add(generated);
            }
        }
    }
}
