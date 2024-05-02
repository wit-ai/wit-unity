/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi
{
    /// <summary>
    /// The various connection types available
    /// </summary>
    public enum WitRequestType
    {
        Http,
        WebSocket
    }

    /// <summary>
    /// Endpoint overrides
    /// </summary>
    public interface IWitRequestEndpointInfo
    {
        // Setup
        string UriScheme { get; }
        string Authority { get; }
        int Port { get; }
        string WitApiVersion { get; }

        // Voice Command Endpoints
        string Message { get; }
        string Speech { get; }
        // Dictation Endpoint
        string Dictation { get; }
        // TTS Endpoint
        string Synthesize { get; }
        // Composer Endpoints
        string Event { get; }
        string Converse { get; }
    }

    /// <summary>
    /// Configuration interface
    /// </summary>
    public interface IWitRequestConfiguration
    {
        /// <summary>
        /// The request connection type to be used by all requests made with this configuration.
        /// </summary>
        WitRequestType RequestType { get; }
        /// <summary>
        /// The request timeout in ms to be used by all requests made with this configuration.
        /// </summary>
        int RequestTimeoutMs { get; }

        string GetConfigurationId();
        string GetApplicationId();
        WitAppInfo GetApplicationInfo();
        WitConfigurationAssetData[] GetConfigData();
        IWitRequestEndpointInfo GetEndpointInfo();
        string GetClientAccessToken();
#if UNITY_EDITOR
        void SetClientAccessToken(string newToken);
        string GetServerAccessToken();
        void SetApplicationInfo(WitAppInfo appInfo);

        void SetConfigData(WitConfigurationAssetData[] configData);
#endif
        /// <summary>
        /// Refreshes the individual data components of the configuration.
        /// </summary>
        void UpdateDataAssets();
    }
}
