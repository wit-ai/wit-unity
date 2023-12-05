/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.WitAi.Events
{
    /// <summary>
    /// An event including raw byte data for outgoing audio streams
    /// <param name="buffer">The byte buffer about to be sent</param>
    /// <param name="offset">The offset into the buffer that should be read</param>
    /// <param name="length">The length of the data to be sent</param>
    /// </summary>
    [Serializable]
    public class WitByteDataEvent : UnityEvent<byte[], int, int> { }

    /// <summary>
    /// An interface with methods that will be triggered when byte data is ready to be sent via a buffer.
    /// </summary>
    public interface IWitByteDataReadyHandler
    {
        /// <summary>
        /// Called when byte data is ready to be sent
        /// </summary>
        /// <param name="buffer">The byte buffer about to be sent</param>
        /// <param name="offset">The offset into the buffer that should be read</param>
        /// <param name="length">The length of the data to be sent</param>
        void OnWitDataReady(byte[] data, int offset, int length);
    }

    public interface IWitByteDataSentHandler
    {
        /// <summary>
        /// Called when byte data has been sent
        /// </summary>
        /// <param name="buffer">The byte buffer about to be sent</param>
        /// <param name="offset">The offset into the buffer that should be read</param>
        /// <param name="length">The length of the data to be sent</param>
        void OnWitDataSent(byte[] data, int offset, int length);
    }
}
