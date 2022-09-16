/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Conduit
{
    /// <summary>
    /// This is used for POST requests.
    /// </summary>
    public class WitFullOutgoingEntity : WitOutgoingEntity
    {
        public List<WitKeyword> Keywords { get; set; }

        public WitFullOutgoingEntity(WitIncomingEntity incoming): base(incoming)
        {
            this.Keywords = incoming.Keywords;
        }
    }
}
