/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi
{
    /// <summary>
    /// A data struct for pooling arrays of a specific count
    /// </summary>
    public class ArrayPool<TElementType> : ObjectPool<TElementType[]>
    {
        /// <summary>
        /// The capacity of each array within this pool
        /// </summary>
        public int Capacity { get; }

        /// <summary>
        /// A constructor that takes a specified
        /// </summary>
        public ArrayPool(int capacity, int preload = 0) : base(() => new TElementType[capacity], preload)
        {
            Capacity = capacity;
        }
    }
}
