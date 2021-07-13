using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Threading.Tasks;

namespace ImageResizer.Plugins.AzureBlobCache.Indexing
{
    public abstract class ConcurrencyHandlingContextBase : DbContext
    {
        protected ConcurrencyHandlingContextBase(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }        

        public virtual int SaveChangesDatabaseWins(int retries = 1)
        {
            return Save(retries, (e) => e.Reload());
        }

        public virtual Task<int> SaveChangesDatabaseWinsAsync(int retries = 1)
        {
            return SaveAsync(retries, (e) => e.Reload());
        }

        public virtual int SaveChangesClientWins(int retries = 1)
        {
            return Save(retries, (e) => e.OriginalValues.SetValues(e.GetDatabaseValues()));
        }

        public virtual Task<int> SaveChangesClientWinsAsync(int retries = 1)
        {
            return SaveAsync(retries, (e) => e.OriginalValues.SetValues(e.GetDatabaseValues()));
        }

        protected virtual async Task<int> SaveAsync(int retries, Action<DbEntityEntry> resolver)
        {
            DbUpdateConcurrencyException exception;
            bool saveFailed;

            do
            {
                try
                {
                    return await SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    exception = ex;
                    saveFailed = true;
                    if (retries-- < 1) throw exception;

                    ResolveConcurrency(exception, resolver);
                }

            } while (saveFailed);

            throw exception;
        }

        protected virtual int Save(int retries, Action<DbEntityEntry> resolver)
        {
            DbUpdateConcurrencyException exception;
            bool saveFailed;

            do
            {
                try
                {
                    return SaveChanges();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    exception = ex;
                    saveFailed = true;
                    if (retries-- < 1) throw exception;

                    ResolveConcurrency(exception, resolver);
                }

            } while (saveFailed);

            throw exception;
        }

        protected virtual void ResolveConcurrency(DbUpdateConcurrencyException exception, Action<DbEntityEntry> resolver)
        {
            foreach (var entry in exception.Entries.ToList())
            {
                resolver(entry);
            }
        }
    }
}
