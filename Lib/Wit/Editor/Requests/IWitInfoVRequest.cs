/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.Threading.Tasks;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi.Requests
{
    internal interface IWitInfoVRequest : IWitVRequest
    {
        Task<bool> RequestAppId(VRequest.RequestCompleteDelegate<string> onComplete);

        Task<bool> RequestApps(int limit, int offset, VRequest.RequestCompleteDelegate<WitAppInfo[]> onComplete);

        Task<bool> RequestAppInfo(string applicationId, VRequest.RequestCompleteDelegate<WitAppInfo> onComplete);

        Task<bool> RequestClientAppToken(string applicationId, VRequest.RequestCompleteDelegate<string> onComplete);

        Task<bool> RequestIntentList(VRequest.RequestCompleteDelegate<WitIntentInfo[]> onComplete);

        Task<bool> RequestIntentInfo(string intentId, VRequest.RequestCompleteDelegate<WitIntentInfo> onComplete);

        Task<bool> RequestEntityList(VRequest.RequestCompleteDelegate<WitEntityInfo[]> onComplete);

        Task<bool> RequestEntityInfo(string entityId, VRequest.RequestCompleteDelegate<WitEntityInfo> onComplete);

        Task<bool> RequestTraitList(VRequest.RequestCompleteDelegate<WitTraitInfo[]> onComplete);

        Task<bool> RequestTraitInfo(string traitId, VRequest.RequestCompleteDelegate<WitTraitInfo> onComplete);

        Task<bool> RequestVoiceList(VRequest.RequestCompleteDelegate<Dictionary<string, WitVoiceInfo[]>> onComplete);
    }
}
