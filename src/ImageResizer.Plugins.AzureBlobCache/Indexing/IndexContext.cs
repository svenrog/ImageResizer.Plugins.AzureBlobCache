using System.Data.Entity;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexContext : ConcurrencyHandlingContextBase
    {
        public IndexContext() : base("ResizerEFConnection") 
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<IndexContext, IndexMigrationConfiguration>());
        }

        public DbSet<IndexEntity> IndexEntities { get; set; }
    }
}
