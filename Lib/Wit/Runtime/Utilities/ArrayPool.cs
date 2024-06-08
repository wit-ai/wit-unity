/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Concurrent;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// A data struct for pooling arrays of a specific type
    /// </summary>
    public class ArrayPool<TElementType>
    {
        /// <summary>
        /// The capacity of each array within this pool
        /// </summary>
        public int Capacity { get; }

        // The currently available arrays
        private readonly ConcurrentQueue<TElementType[]> _available = new ConcurrentQueue<TElementType[]>();

        /// <summary>
        /// A constructor that takes a specified
        /// </summary>
        public ArrayPool(int capacity, int preload = 0)
        {
            Capacity = capacity;
            Preload(preload);
        }

        /// <summary>
        /// Take an array from the pool or generate if needed
        /// </summary>
        public TElementType[] Load()
        {
            if (_available.TryDequeue(out var newArray))
            {
                return newArray;
            }
            return new TElementType[Capacity];
        }

        /// <summary>
        /// Return an element to the pool
        /// </summary>
        public void Unload(TElementType[] oldArray)
        {
            _available.Enqueue(oldArray);
        }

        /// <summary>
        /// Load a specified amount and then unloads them all
        /// </summary>
        public void Preload(int total)
        {
            if (total <= 0)
            {
                return;
            }
            TElementType[][] preloads = new TElementType[total][];
            for (int i = 0; i < total; i++)
            {
                preloads[i] = Load();
            }
            for (int i = 0; i < total; i++)
            {
                Unload(preloads[i]);
            }
        }
    }
}
