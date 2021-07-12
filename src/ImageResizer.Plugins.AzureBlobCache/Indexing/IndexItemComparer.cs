using System.Collections.Generic;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexItemComparer : IEqualityComparer<IndexItem>
    {
        public bool Equals(IndexItem x, IndexItem y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return true;

            if (x.Key.Equals(y.Key) == false) return false;
            if (x.Modified.Equals(y.Modified) == false) return false;

            return true;
        }

        public int GetHashCode(IndexItem obj)
        {
            return obj?.Key.GetHashCode() ?? 0;
        }
    }
}
