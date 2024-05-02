/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if UNITY_EDITOR
using System;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi
{
    /// <summary>
    /// A simple configuration for initial setup
    /// </summary>
    public class WitServerRequestConfiguration : IWitRequestConfiguration, IWitRequestEndpointInfo
    {
        private string _clientToken;
        private string _serverToken;
        private WitAppInfo _appInfo;
        private WitConfigurationAssetData[] _configurationData =  Array.Empty<WitConfigurationAssetData>();

        public WitServerRequestConfiguration(string serverToken)
        {
            _serverToken = serverToken;
            _appInfo = new WitAppInfo();
        }

        public string GetConfigurationId() => null;
        public string GetApplicationId() => _appInfo.id;
        public WitAppInfo GetApplicationInfo() => _appInfo;
        public void SetApplicationInfo(WitAppInfo newInfo) => _appInfo = newInfo;
        public WitConfigurationAssetData[] GetConfigData() => _configurationData;

        public void SetConfigData(WitConfigurationAssetData[] configData)
        {
            _configurationData = configData;
        }
        public void UpdateDataAssets()
        {
            // Nothing to do by default.
        }

        public string GetClientAccessToken() => _clientToken;
        public void SetClientAccessToken(string newToken) => _clientToken = newToken;
        public string GetServerAccessToken() => _serverToken;

        // Endpoint info
        public IWitRequestEndpointInfo GetEndpointInfo() => this;
        public WitRequestType RequestType => WitRequestType.Http;
        public int RequestTimeoutMs => WitConstants.DEFAULT_REQUEST_TIMEOUT;
        public string UriScheme => WitConstants.URI_SCHEME;
        public string Authority => WitConstants.URI_AUTHORITY;
        public string WitApiVersion => WitConstants.API_VERSION;
        public int Port => WitConstants.URI_DEFAULT_PORT;
        public string Message => WitConstants.ENDPOINT_MESSAGE;
        public string Speech => WitConstants.ENDPOINT_SPEECH;
        public string Dictation => WitConstants.ENDPOINT_DICTATION;
        public string Synthesize => WitConstants.ENDPOINT_TTS;
        public string Event => WitConstants.ENDPOINT_COMPOSER_MESSAGE;
        public string Converse => WitConstants.ENDPOINT_COMPOSER_SPEECH;
    }
}
#endif
