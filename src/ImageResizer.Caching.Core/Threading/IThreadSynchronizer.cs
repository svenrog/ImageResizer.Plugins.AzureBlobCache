using System;
using System.Collections.Generic;

namespace ImageResizer.Caching.Core.Threading
{
    public interface IThreadSynchronizer<in TKey, out TValue>
    {
        /// <summary>
        /// Gets or creates a <see cref="TValue"/> for <see cref="TKey"/> key.
        /// </summary>
        /// <param name="key"><see cref="TKey"/> key for synchronizer.</param>
        /// <returns>An instance of <see cref="TValue"/></returns>
        /// <exception cref="ArgumentNullException">If the key is null.</exception>
        TValue this[TKey key] { get; }

        /// <summary>
        /// Checks if the synchronizer contains <see cref="TKey" />.
        /// </summary>
        /// <param name="key"><see cref="TKey"/> key for synchronizer.</param>
        /// <returns><see cref="bool"/> indicating if <see cref="TKey"/> is contained.</returns>
        /// <exception cref="ArgumentNullException">If the key is null.</exception>
        bool Contains(TKey key);

        /// <summary>
        /// Adds a new instance of <see cref="TValue"/> for <see cref="TKey"/> key.
        /// </summary>
        /// <param name="key"><see cref="TKey"/> key for synchronizer.</param>
        /// <exception cref="ArgumentException">If the is already contained.</exception>
        /// <exception cref="ArgumentNullException">If the key is null.</exception>
        void Add(TKey key);

        /// <summary>
        /// Removes an instance of <see cref="TValue"/> for <see cref="TKey"/> key.
        /// </summary>
        /// <param name="key"><see cref="TKey"/> key for synchronizer.</param>
        /// <exception cref="KeyNotFoundException">If the key is not contained.</exception>
        /// <exception cref="ArgumentNullException">If the key is null.</exception>
        void Remove(TKey key);

        /// <summary>
        /// Tries to remove an instance of <see cref="TValue"/> for <see cref="TKey"/> key.
        /// </summary>
        /// <param name="key"><see cref="TKey"/> key for synchronizer.</param>
        /// <returns><see cref="bool"/> indicating if <see cref="TKey"/> is removed by method call.</returns>
        bool TryRemove(TKey key);
    }

    public interface IThreadSynchronizer<in TKey> : IThreadSynchronizer<TKey, object> { }
}
