using System.Data.Entity.Migrations;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    internal sealed class IndexMigrationConfiguration : DbMigrationsConfiguration<IndexContext>
    {
        public IndexMigrationConfiguration()
        {
            AutomaticMigrationsEnabled = true;
            ContextKey = "ImageResizer.Plugins.AzureBlobCache.Indexing.IndexContext";
        }

        protected override void Seed(IndexContext context)
        {

        }
    }
}
