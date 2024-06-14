/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// A simple interface for providing WitWebSocketClient
    /// </summary>
    public interface IWitWebSocketClientProvider
    {
        /// <summary>
        /// The web socket client to be used
        /// </summary>
        public IWitWebSocketClient WebSocketClient { get; }
    }
}
