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
        #if UNITY_2021_3_2 || UNITY_2021_3_3 || UNITY_2021_3_4 || UNITY_2021_3_5
        [NonReorderable]
        #endif
        public List<string> synonyms;
    }
}
