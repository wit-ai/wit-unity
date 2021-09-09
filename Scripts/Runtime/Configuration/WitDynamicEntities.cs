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
    public class WitDynamicEntities : IDynamicEntitiesProvider
    {
        public WitResponseClass entities;

        public WitDynamicEntities()
        {
            entities = new WitResponseClass();
        }

        public void Add(WitSimpleDynamicEntity entity)
        {
            KeyValuePair<string, WitResponseArray> pair = entity.GetEntityPair();
            entities.Add(pair.Key, pair.Value);
        }

        public void Add(WitDynamicEntity entity)
        {
            KeyValuePair<string, WitResponseArray> pair = entity.GetEntityPair();
            entities.Add(pair.Key, pair.Value);
        }

        public string ToJSON()
        {
            return entities.ToString();
        }
    }
}
