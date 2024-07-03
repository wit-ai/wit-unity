/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;

namespace Meta.WitAi
{
    /// <summary>
    /// A data struct for pooling objects for reuse
    /// </summary>
    public class ObjectPool<T> : IDisposable
    {
        /// <summary>
        /// The method used to generate objects
        /// </summary>
        private readonly Func<T> _generator;

        /// <summary>
        /// The concurrent bag of objects that are available
        /// </summary>
        private readonly ConcurrentBag<T> _available;

        /// <summary>
        /// A constructor that takes a specified object generation function
        /// </summary>
        public ObjectPool(Func<T> generator, int preload = 0)
        {
            // Set generator or throw error
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            // Set available bag
            _available = new ConcurrentBag<T>();
            // Preload if desired
            Preload(preload);
        }

        /// <summary>
        /// Dispose when unloaded
        /// </summary>
        ~ObjectPool()
        {
            Dispose();
        }

        /// <summary>
        /// Take an object from the pool or generate if none are available
        /// </summary>
        public T Get()
        {
            if (_available.TryTake(out var item))
            {
                return item;
            }
            return _generator();
        }

        /// <summary>
        /// Return an item to the pool
        /// </summary>
        public void Return(T item)
        {
            _available.Add(item);
        }

        /// <summary>
        /// Generates and returns a specified amount of objects
        /// </summary>
        public void Preload(int total)
        {
            if (total <= 0)
            {
                return;
            }
            for (int i = 0; i < total; i++)
            {
                Return(_generator());
            }
        }

        /// <summary>
        /// Immediately unload all available items
        /// </summary>
        public void Dispose()
        {
            _available.Clear();
        }
    }
}
