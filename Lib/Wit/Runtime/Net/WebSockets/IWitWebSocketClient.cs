/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.Voice.Net.PubSub;

namespace Meta.Voice.Net.WebSockets
{
    public interface IWitWebSocketClient : IPubSubSubscriber
    {
        /// <summary>
        /// The settings required to connect, authenticate and drive server/client communication.
        /// </summary>
        WitWebSocketSettings Settings { get; }

        /// <summary>
        /// Whether the web socket is disconnected, connecting, connected, or disconnecting.
        /// </summary>
        WitWebSocketConnectionState ConnectionState { get; }

        /// <summary>
        /// Whether authentication had completed successfully or not
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Whether there currently is data being encoded and/or queued to be sent from the web socket.
        /// </summary>
        bool IsUploading { get; }

        /// <summary>
        /// Whether there currently is data being received and/or decoded from the web socket.
        /// </summary>
        bool IsDownloading { get; }

        /// <summary>
        /// Whether there currently are any scripts that have called Connect()
        /// and not yet requested a Disconnect().
        /// </summary>
        bool IsReferenced { get; }

        /// <summary>
        /// Whether will be reconnecting
        /// </summary>
        bool IsReconnecting { get; }

        /// <summary>
        /// Total amount of scripts that have called Connect()
        /// and have not yet called Disconnect().  Used to ensure
        /// WebSocketClient is only disconnected once no scripts are still referenced.
        /// </summary>
        int ReferenceCount { get; }

        /// <summary>
        /// Total amount of failed connection attempts made
        /// </summary>
        int FailedConnectionAttempts { get; }

        /// <summary>
        /// The utc time of the last response from the server
        /// </summary>
        DateTime LastResponseTime { get; }

        /// <summary>
        /// The requests currently being tracked by this client. Each access generates
        /// a new dictionary and should be cached.
        /// </summary>
        Dictionary<string, IWitWebSocketRequest> Requests { get; }

        /// <summary>
        /// Callback on connection state change.
        /// </summary>
        event Action<WitWebSocketConnectionState> OnConnectionStateChanged;

        /// <summary>
        /// Attempts to connect to the specified
        /// </summary>
        void Connect();

        /// <summary>
        /// Disconnects socket after checking state
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Send a request via this client if possible
        /// </summary>
        bool SendRequest(IWitWebSocketRequest request);

        /// <summary>
        /// Safely adds a request to the current request list
        /// </summary>
        bool TrackRequest(IWitWebSocketRequest request);

        /// <summary>
        /// Safely removes a request from the current request list
        /// </summary>
        bool UntrackRequest(IWitWebSocketRequest request);

        /// <summary>
        /// Safely removes a request from the current request list by request id
        /// </summary>
        bool UntrackRequest(string requestId);

        /// <summary>
        /// Callback when a tracked topic generates a request
        /// </summary>
        event Action<string, IWitWebSocketRequest> OnTopicRequestTracked;

        /// <summary>
        /// Method to subscribe to a specific topic id with a parameter to ignore
        /// ref count for local unsubscribing following disconnect/error.
        /// </summary>
        void Unsubscribe(string topicId, bool ignoreRefCount);
    }
}
