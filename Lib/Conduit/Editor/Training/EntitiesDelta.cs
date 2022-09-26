/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.WitAi.Data.Info;

namespace Meta.Conduit.Editor
{
    public class EntitiesDelta
    {
        public List<WitEntityKeywordInfo> InWitOnly;
        public List<string> InLocalOnly;

        public bool IsEmpty => InLocalOnly.Count == 0 && InWitOnly.Count == 0;
    }
}
