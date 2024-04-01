/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Json;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An interface for generating new requests when responses return
    /// from the server without currently tracked request ids.
    /// </summary>
    public interface IWitWebSocketRequestProvider
    {
        /// <summary>
        /// A method for generating the desired web socket
        /// request using the returned json data & binary data
        /// </summary>
        public IWitWebSocketRequest GenerateWebSocketRequest(string requestId, WitResponseNode jsonData);
    }
}
