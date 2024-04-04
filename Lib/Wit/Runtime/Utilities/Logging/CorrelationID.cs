/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// The correlation ID allows the tracing of an operation from beginning to end.
    /// It can be linked to other IDs to form a full chain when it branches out or moves to other domains.
    /// If not supplied explicitly while logging, it will be inherited from the thread storage or a
    /// new one will be generated if none exist.
    /// </summary>
    public readonly struct CorrelationID
    {
        private string Value { get; }

        private CorrelationID(string value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns true if the correlation ID has a value.
        /// </summary>
        public bool IsAssigned => Value != null;

        public override string ToString() => Value;

        public static implicit operator string(CorrelationID correlationId) => correlationId.Value;
        public static explicit operator CorrelationID(string value) => new CorrelationID(value);
        public static implicit operator CorrelationID(Guid value) => new CorrelationID(value.ToString());

        public override bool Equals(object obj) => obj is CorrelationID other && Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
    }
}
