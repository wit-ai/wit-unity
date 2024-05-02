/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi
{
    /// <summary>
    /// A custom configuration class that can be used without the overhead caused using WitConfiguration.
    /// Used by 1st and 3rd party developers.
    /// </summary>
    public class WitRuntimeRequestConfiguration : IWitRequestConfiguration, IWitRequestEndpointInfo
    {
        private WitAppInfo _appInfo;
        private string _userToken;
        private WitConfigurationAssetData[] _configurationData =  Array.Empty<WitConfigurationAssetData>();

        public WitRuntimeRequestConfiguration(string userToken)
        {
            _userToken = userToken;
            _appInfo = new WitAppInfo();
        }

        public string GetConfigurationId() => null;

        public string GetApplicationId() => _appInfo.id;
        public WitAppInfo GetApplicationInfo() => _appInfo;

        public WitConfigurationAssetData[] GetConfigData() => _configurationData;

        public IWitRequestEndpointInfo GetEndpointInfo() => this;

        public string GetClientAccessToken() => _userToken;
        public void SetClientAccessToken(string newToken) => _userToken = newToken;

        public string GetServerAccessToken()
        {
            throw new System.NotImplementedException();
        }

        public void SetApplicationInfo(WitAppInfo newInfo) => _appInfo = newInfo;

        public void SetConfigData(WitConfigurationAssetData[] configData)
        {
            _configurationData = configData;
        }

        public void UpdateDataAssets()
        {
            // Nothing to do by default.
        }

        // Endpoint info
        public WitRequestType RequestType { get; set; } = WitConstants.DEFAULT_REQUEST_TYPE;
        public int RequestTimeoutMs => WitConstants.DEFAULT_REQUEST_TIMEOUT;
        public string UriScheme => WitConstants.URI_SCHEME;
        public string Authority => WitConstants.URI_GRAPH_AUTHORITY;
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
