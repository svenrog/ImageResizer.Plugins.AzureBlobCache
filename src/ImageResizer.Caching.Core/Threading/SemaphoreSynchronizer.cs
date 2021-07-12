using System;
using System.Threading;

namespace ImageResizer.Caching.Core.Threading
{
    public class SemaphoreSynchronizer<TKey> : ThreadSynchronizerBase<TKey, SemaphoreSlim>, IDisposable
    {
        protected readonly int SemaphoreMaxCount;
        private bool _disposed;

        public SemaphoreSynchronizer()
        {
            SemaphoreMaxCount = 1;
            _disposed = false;
        }

        ~SemaphoreSynchronizer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            lock (KeyLock)
            {
                foreach (var value in Index.Values)
                {
                    Dispose(value);
                }

                Index.Clear();
            }

            _disposed = true;
        }

        protected virtual void Dispose(SemaphoreSlim value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            value.Dispose();
        }

        public override void Remove(TKey key)
        {
            lock (KeyLock)
            {
                var value = Index[key];

                Dispose(value);

                Index.Remove(key);
            }
        }

        public override bool TryRemove(TKey key)
        {
            if (key == null)
                return false;

            if (!Index.ContainsKey(key))
                return false;

            lock (KeyLock)
            {
                var value = Index[key];
                if (value.CurrentCount < SemaphoreMaxCount)
                {
                    return false;
                }
            }

            try
            {
                Remove(key);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected override SemaphoreSlim Create(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            return new SemaphoreSlim(SemaphoreMaxCount, SemaphoreMaxCount);
        }
    }
}
