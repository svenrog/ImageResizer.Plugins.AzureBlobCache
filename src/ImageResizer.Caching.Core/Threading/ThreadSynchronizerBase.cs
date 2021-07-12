using System;
using System.Collections.Generic;

namespace ImageResizer.Caching.Core.Threading
{
    public abstract class ThreadSynchronizerBase<TKey> : ThreadSynchronizerBase<TKey, object>, IThreadSynchronizer<TKey> { }
    public abstract class ThreadSynchronizerBase<TKey, TValue> : IThreadSynchronizer<TKey, TValue>
    {
        protected readonly IDictionary<TKey, TValue> Index;
        protected readonly object KeyLock;

        protected ThreadSynchronizerBase()
        {
            KeyLock = new object();
            Index = new Dictionary<TKey, TValue>();
        }

        public virtual TValue this[TKey key]
        {
            get
            {
                lock (KeyLock)
                {
                    if (Index.TryGetValue(key, out var result))
                    {
                        return result;
                    }

                    return Index[key] = Create(key);
                }
            }
        }

        public virtual bool Contains(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            lock (KeyLock)
            {
                return Index.ContainsKey(key);
            }
        }

        public virtual void Add(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            lock (KeyLock)
            {
                Index.Add(key, Create(key));
            }
        }

        public virtual void Remove(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            lock (KeyLock)
            {
                Index.Remove(key);
            }
        }

        public virtual bool TryRemove(TKey key)
        {
            if (key == null)
                return false;

            if (!Index.ContainsKey(key))
                return false;

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

        protected abstract TValue Create(TKey value);
    }
}
