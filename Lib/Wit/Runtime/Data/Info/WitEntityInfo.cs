/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine;

namespace Meta.WitAi.Data.Info
{
    [Serializable]
    public struct WitEntityInfo
    {
        /// <summary>
        /// Entity display name
        /// </summary>
        [SerializeField] public string name;
        /// <summary>
        /// Entity unique identifier
        /// </summary>
        [SerializeField] public string id;
        /// <summary>
        /// Various lookup options for this entity
        /// </summary>
        #if UNITY_2021_3_2 || UNITY_2021_3_3 || UNITY_2021_3_4 || UNITY_2021_3_5
        [NonReorderable]
        #endif
        [SerializeField] public string[] lookups;
        /// <summary>
        /// Various roles in which this
        /// entity may be used
        /// </summary>
        [SerializeField] public WitEntityRoleInfo[] roles;
        /// <summary>
        /// Mapped keywords and their analyzed
        /// synonyms
        /// </summary>
        [SerializeField] public WitEntityKeywordInfo[] keywords;
    }
}
