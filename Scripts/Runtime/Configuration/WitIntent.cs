﻿/*
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
    public class WitIntent : WitConfigurationData
    {
        [SerializeField] public string id;
        [SerializeField] public string name;
        [SerializeField] public WitEntity[] entities;

        protected override WitRequest OnCreateRequest()
        {
            return witConfiguration.GetIntentRequest(name);
        }

        protected override void Update(WitResponseNode entityWitResponse)
        {
            id = entityWitResponse["id"].Value;
            name = entityWitResponse["name"].Value;
            var entityArray = entityWitResponse["entities"].AsArray;
            entities = new WitEntity[entityArray.Count];
            for (int i = 0; i < entityArray.Count; i++)
            {
                entities[i] = WitEntity.FromJson(entityArray[i]);
            }
        }

        public static WitIntent FromJson(WitResponseNode intentWitResponse)
        {
            var intent = new WitIntent();
            intent.Update(intentWitResponse);
            return intent;
        }
    }
}
