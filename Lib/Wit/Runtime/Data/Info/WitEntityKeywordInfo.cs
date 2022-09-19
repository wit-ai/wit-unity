/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;

namespace Meta.WitAi.Data.Info
{
    [Serializable]
    public struct WitEntityKeywordInfo
    {
        /// <summary>
        /// Unique keyword identifier
        /// </summary>
        public string keyword;
        /// <summary>
        /// Synonyms for specified keyword
        /// </summary>
        public List<string> synonyms;
    }
}
