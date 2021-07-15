using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexEntityComparer : IEqualityComparer<IndexEntity>
    {
        public bool Equals(IndexEntity x, IndexEntity y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return true;

            if (x.Key.Equals(y.Key) == false) return false;
            return true;
        }

        public int GetHashCode(IndexEntity obj)
        {
            return obj?.Key.GetHashCode() ?? 0;
        }
    }
}
