/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using com.facebook.witai.interfaces;
using com.facebook.witai.lib;

namespace com.facebook.witai
{
    public class WitFullEntityList : IEntityListProvider
    {
        public string entity;
        public Dictionary<string, List<string>> keywordsToSynonyms;

        public WitFullEntityList(string entity, Dictionary<string, List<string>> keywordsToSynonyms)
        {
            this.entity = entity;
            this.keywordsToSynonyms = keywordsToSynonyms;
        }

        public string ToJSON()
        {
            var keywordEntries = new WitResponseArray();
            foreach (var keywordToSynonyms in keywordsToSynonyms)
            {
                var synonyms = new WitResponseArray();
                foreach (string synonym in keywordToSynonyms.Value)
                {
                    synonyms.Add(new WitResponseData(synonym));
                }

                var keywordEntry = new WitResponseClass();
                keywordEntry.Add("keyword", new WitResponseData(keywordToSynonyms.Key));
                keywordEntry.Add("synonyms", synonyms);

                keywordEntries.Add(keywordEntry);
            }

            var root = new WitResponseClass();
            root.Add(entity, keywordEntries);

            return root.ToString();
        }
    }
}
