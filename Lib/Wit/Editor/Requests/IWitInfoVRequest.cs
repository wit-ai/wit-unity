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
        Task<VRequestResponse<string>> RequestAppId();

        Task<VRequestResponse<WitAppInfo[]>> RequestApps(int limit, int offset);

        Task<VRequestResponse<WitAppInfo>> RequestAppInfo(string applicationId);

        Task<VRequestResponse<WitExportInfo>> RequestAppExportInfo(string applicationId);

        Task<VRequestResponse<WitVersionTagInfo[][]>> RequestAppVersionTags(string applicationId);

        Task<VRequestResponse<string>> RequestClientToken(string applicationId);

        Task<VRequestResponse<WitIntentInfo[]>> RequestIntentList();

        Task<VRequestResponse<WitIntentInfo>> RequestIntentInfo(string intentId);

        Task<VRequestResponse<WitEntityInfo[]>> RequestEntityList();

        Task<VRequestResponse<WitEntityInfo>> RequestEntityInfo(string entityId);

        Task<VRequestResponse<WitTraitInfo[]>> RequestTraitList();

        Task<VRequestResponse<WitTraitInfo>> RequestTraitInfo(string traitId);

        Task<VRequestResponse<Dictionary<string, WitVoiceInfo[]>>> RequestVoiceList();
    }
}
