/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Facebook.WitAi.Interfaces;
using Facebook.WitAi.Lib;

namespace Facebook.WitAi.Data.Entities
{
    [Serializable]
    public class WitDynamicEntities : IDynamicEntitiesProvider, IEnumerable<WitDynamicEntity>
    {
        public List<WitDynamicEntity> entities = new List<WitDynamicEntity>();

        public WitDynamicEntities()
        {

        }

        public WitDynamicEntities(params WitDynamicEntity[] entity)
        {
            entities.AddRange(entity);
        }

        public WitResponseClass AsJson
        {
            get
            {
                WitResponseClass json = new WitResponseClass();
                foreach (var entity in entities)
                {
                    json.Add(entity.entity, entity.AsJson);
                }

                return json;
            }
        }


        public override string ToString()
        {
            return AsJson.ToString();
        }

        public IEnumerator<WitDynamicEntity> GetEnumerator() => entities.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public WitDynamicEntities GetDynamicEntities()
        {
            return this;
        }

        public void Merge(IDynamicEntitiesProvider provider)
        {
            if (null == provider) return;

            entities.AddRange(provider.GetDynamicEntities());
        }

        public void Merge(IEnumerable<WitDynamicEntity> mergeEntities)
        {
            if (null == mergeEntities) return;

            entities.AddRange(mergeEntities);
        }
    }
}
