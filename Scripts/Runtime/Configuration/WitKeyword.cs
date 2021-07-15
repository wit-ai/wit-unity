/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai.data
{
    [Serializable]
    public class WitKeyword
    {
        [SerializeField] public WitConfiguration witConfiguration;
        [SerializeField] public string keyword;
        [SerializeField] public string[] synonyms;

        #if UNITY_EDITOR
        public static WitKeyword FromJson(WitResponseNode keywordNode)
        {
            return new WitKeyword()
            {
                keyword = keywordNode["keyword"],
                synonyms = keywordNode["synonyms"].AsStringArray
            };
        }
        #endif
    }
}
