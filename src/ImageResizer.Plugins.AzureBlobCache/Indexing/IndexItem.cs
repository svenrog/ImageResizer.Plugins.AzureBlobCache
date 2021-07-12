using System;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexItem : IComparable<IndexItem>
    {
        public IndexItem(Guid key, DateTime modified, long size)
        {
            Key = key;
            Modified = modified;
            Size = size;
        }

        public readonly Guid Key;
        public readonly DateTime Modified;
        public readonly long Size;

        public int CompareTo(IndexItem other)
        {
            return Modified.CompareTo(other.Modified);
        }
    }
}
