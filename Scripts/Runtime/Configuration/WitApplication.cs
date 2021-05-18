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
    public class WitApplication : WitConfigurationData
    {
        [SerializeField] public string name;
        [SerializeField] public string id;
        [SerializeField] public string lang;
        [SerializeField] public bool isPrivate;
        [SerializeField] public string createdAt;

        protected override WitRequest OnCreateRequest()
        {
            return witConfiguration.GetAppRequest(id);
        }

        protected override void Update(JSONNode appJson)
        {
            id = appJson["id"].Value;
            name = appJson["name"].Value;
            lang = appJson["lang"].Value;
            isPrivate = appJson["private"].AsBool;
            createdAt = appJson["created_at"].Value;
        }

        public static WitApplication FromJson(JSONNode appJson)
        {
            var app = new WitApplication();
            app.Update(appJson);
            return app;
        }
    }
}
