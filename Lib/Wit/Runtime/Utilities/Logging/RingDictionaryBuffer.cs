/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// This class will maintain a cache of entries and the oldest ones will expire when it runs out of space.
    /// Each time an item is added to a key, that key's freshness is refreshed.
    /// Each key is associated with a list of entries.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    internal class RingDictionaryBuffer<TKey, TValue>
    {
        private readonly int _capacity;
        private readonly ConcurrentDictionary<TKey, LinkedList<TValue>> _dictionary = new();
        private readonly ConcurrentDictionary<TKey, Object> _valueLocks = new();
        public RingDictionaryBuffer(int capacity)
        {
            _capacity = capacity;
        }

        public ICollection<TValue> this[TKey key] => _dictionary[key];

        /// <summary>
        /// Adds an entry to the key. This also updates the "freshness" of the entry.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to add.</param>
        /// <param name="unique">Will only add the value if it does not already exist.</param>
        /// <returns>True if the key value was added. False otherwise.</returns>
        public bool Add(TKey key, TValue value, bool unique = false)
        {
            // Generate list & lock if needed
            if (!_dictionary.TryGetValue(key, out var list))
            {
                list = new LinkedList<TValue>();
                _dictionary[key] = list;
            }
            if (!_valueLocks.TryGetValue(key, out var listLock))
            {
                listLock = new Object();
                _valueLocks[key] = listLock;
            }

            bool added = true;
            lock (listLock)
            {
                // If unique, remove previous to resort to the start
                if (unique && list.Contains(value))
                {
                    added = false;
                    list.Remove(value);
                }
                // Add to beginning
                list.AddFirst(value);
                // Remove any extra
                while (list.Count > UnityEngine.Mathf.Max(0, _capacity))
                {
                    list.RemoveLast();
                }
            }
            return added;
        }

        /// <summary>
        /// Returns true if the key exists in the buffer.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists in the buffer. False otherwise.</returns>
        public bool ContainsKey(TKey key)
        {
            return _dictionary.ContainsKey(key);
        }

        /// <summary>
        /// Drain all the entries from the buffer that match a given key and return them.
        /// </summary>
        /// <param name="key">The key we are extracting.</param>
        /// <returns>All the entries in the buffer for that specific key.</returns>
        public IEnumerable<TValue> Extract(TKey key)
        {
            _valueLocks.TryRemove(key, out var discard);
            if (!_dictionary.TryRemove(key, out var list))
            {
                return new TValue[] {};
            }
            return list;
        }

        /// <summary>
        /// Drain all the entries from the buffer and return them.
        /// </summary>
        /// <returns>All the entries in the buffer ordered by the key (e.g. correlation IDs).</returns>
        public IEnumerable<TValue> ExtractAll()
        {
            var allValues = new List<TValue>();
            foreach (var correlationId in new List<TKey>(_dictionary.Keys))
            {
                allValues.AddRange(Extract(correlationId));
            }
            return allValues;
        }

        /// <summary>
        /// Clears the buffer.
        /// </summary>
        public void Clear()
        {
            _dictionary.Clear();
            _valueLocks.Clear();
        }
    }
}
