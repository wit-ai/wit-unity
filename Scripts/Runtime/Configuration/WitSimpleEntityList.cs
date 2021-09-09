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
    public class WitSimpleEntityList : IEntityListProvider
    {
        public List<string> keywords;
        public string entity;

        public WitSimpleEntityList(string entityIdentifier, List<string> words)
        {
            entity = entityIdentifier;
            keywords = words;
        }

        public string ToJSON()
          {
            var allEntitiesArray = new WitResponseArray();
            var root = new WitResponseClass();
            root.Add(entity, allEntitiesArray);
            foreach (string keyword in keywords)
            {
                var keywordJson = new WitResponseClass();
                var synonymsArray = new WitResponseArray();
                keywordJson.Add("keyword",new WitResponseData(keyword));
                synonymsArray.Add(new WitResponseData(keyword));
                keywordJson.Add("synonyms",synonymsArray);
                allEntitiesArray.Add(keywordJson);
            }
            return root.ToString();
          }
    }
}
