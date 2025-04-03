
/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Voice.Net.WebSockets
{
    /// <summary>
    /// An interface for obtaining a custom web socket
    /// </summary>
    public interface IWebSocketProvider
    {
        /// <summary>
        /// Method call for obtaining a web socket client
        /// </summary>
        IWebSocket GetWebSocket(string url, Dictionary<string, string> headers);
    }
}
