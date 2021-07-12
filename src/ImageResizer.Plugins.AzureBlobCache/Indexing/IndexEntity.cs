using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{

    public class IndexEntity
    {
        public IndexEntity() { }
        public IndexEntity(Guid key, DateTime modified, long size)
        {
            Key = key;
            Modified = modified;
            Size = size;
        }

        [Key]
        public Guid Key { get; set; }

        [Index("IX_Modified", IsClustered = false)]
        public DateTime Modified { get; set; }

        public long Size { get; set; }
    }
}
