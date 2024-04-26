/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
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
        private readonly Dictionary<TKey, LinkedList<TValue>> _dictionary;
        private readonly LinkedList<ValueTuple<TKey, TValue>> _order;
        public RingDictionaryBuffer(int capacity)
        {
            _capacity = capacity;
            _dictionary = new Dictionary<TKey, LinkedList<TValue>>();
            _order = new LinkedList<ValueTuple<TKey, TValue>>();
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
            if (!_dictionary.ContainsKey(key))
            {
                _dictionary[key] = new LinkedList<TValue>();
            }

            if (unique && _dictionary[key].Contains(value))
            {
                return false;
            }

            _dictionary[key].AddLast(value);
            _order.AddLast(ValueTuple.Create(key, value));
            if (_order.Count > _capacity)
            {
                var oldest = _order.First.Value;
                _order.RemoveFirst();
                _dictionary[oldest.Item1].RemoveFirst();
                if (_dictionary[oldest.Item1].Count == 0)
                {
                    _dictionary.Remove(oldest.Item1);
                }
            }

            return true;
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
            if (_dictionary.ContainsKey(key))
            {
                var values = new List<TValue>(_dictionary[key]);
                _dictionary.Remove(key);
                var node = _order.First;
                while (node != null)
                {
                    var nextNode = node.Next; // Save next node
                    if (node.Value.Item1.Equals(key))
                    {
                        _order.Remove(node); // Remove current node
                    }
                    node = nextNode; // Move to next node
                }
                return values;
            }
            else
            {
                return new List<TValue>();
            }
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
            _order.Clear();
        }
    }
}
