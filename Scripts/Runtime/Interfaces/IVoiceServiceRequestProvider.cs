/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.Configuration;
using Meta.WitAi.Requests;

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// An interface for creating voice service requests providing all specified parameters
    /// </summary>
    public interface IVoiceServiceRequestProvider
    {
        VoiceServiceRequest CreateRequest(WitRuntimeConfiguration requestSettings, WitRequestOptions requestOptions, VoiceServiceRequestEvents requestEvents);
    }
}
