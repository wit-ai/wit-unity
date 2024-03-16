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
    /// An error code that is used to group similar errors together and provide actionable feedback.
    /// </summary>
    public readonly struct ErrorCode
    {
        private string Value { get; }

        private ErrorCode(string value)
        {
            Value = value;
        }

        public override string ToString() => Value;

        public static implicit operator string(ErrorCode errorCode) => errorCode.Value;
        public static explicit operator ErrorCode(string value) => new ErrorCode(value);
        public static implicit operator ErrorCode(KnownErrorCode value) => new ErrorCode(value.ToString());

        public override bool Equals(object obj) => obj is ErrorCode other && Value == other.Value;
        public override int GetHashCode() => Value.GetHashCode();
    }
}
