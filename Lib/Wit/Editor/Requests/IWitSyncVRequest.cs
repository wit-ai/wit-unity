/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Threading.Tasks;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Json;

namespace Meta.WitAi.Requests
{
    internal interface IWitSyncVRequest : IWitVRequest
    {
        Task<VRequestResponse<WitIntentInfo>> RequestAddIntent(WitIntentInfo intentInfo);

        Task<VRequestResponse<WitEntityInfo>> RequestAddEntity(WitEntityInfo entityInfo);

        Task<VRequestResponse<WitEntityInfo>> RequestAddEntityKeyword(string entityId,
            WitEntityKeywordInfo keywordInfo);

        Task<VRequestResponse<WitEntityInfo>> RequestAddEntitySynonym(string entityId, string keyword, string synonym);

        Task<VRequestResponse<WitTraitInfo>> RequestAddTrait(WitTraitInfo traitInfo);

        Task<VRequestResponse<WitTraitInfo>> RequestAddTraitValue(string traitId,
            string traitValue);

        Task<VRequestResponse<WitResponseNode>> RequestImportData(string manifestData);
    }
}
