using System.Data.Entity;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public class IndexContext : DbContext
    {
        public IndexContext() : base("ResizerEFConnection") 
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<IndexContext, IndexMigrationConfiguration>());
        }

        public IndexContext(string connectionName) : base(connectionName) 
        {
            Database.SetInitializer(new MigrateDatabaseToLatestVersion<IndexContext, IndexMigrationConfiguration>());
        }

        public DbSet<IndexEntity> IndexEntities { get; set; }
    }
}
