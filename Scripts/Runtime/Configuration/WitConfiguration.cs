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
    [CreateAssetMenu(fileName = "WitConfiguration", menuName = "Wit/Configuration", order = 1)]
    public class WitConfiguration : ScriptableObject
    {
        [SerializeField] public string clientAccessToken;

        [SerializeField] public int activeApplication;
        [SerializeField] public string[] applicationNames;
        [SerializeField] public WitApplication[] applications;
        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;

        public WitApplication ActiveApplication
        {
            get
            {
                WitApplication app = null;
                if (activeApplication >= 0 && activeApplication < applications.Length)
                {
                    app = applications[activeApplication];
                }
                return app;
            }
        }

        public void Update()
        {
            var request = this.ListAppsRequest(10);
            request.onResponse = (r) => OnUpdate(r, UpdateAppList);
            request.Request();

            request = this.ListIntentsRequest();
            request.onResponse = (r) => OnUpdate(r, UpdateIntentList);
            request.Request();

            request = this.ListEntitiesRequest();
            request.onResponse = (r) => OnUpdate(r, UpdateEntityList);
            request.Request();
        }

        private void OnUpdate(WitRequest request, Action<JSONNode> updateComponent)
        {
            if (request.StatusCode == 200)
            {
                updateComponent(request.ResponseData);
            }
            else
            {
                Debug.LogError(request.StatusDescription);
            }
        }

        private void UpdateIntentList(JSONNode intentListJson)
        {
            var intentList = intentListJson.AsArray;
            intents = new WitIntent[intentList.Count];
            for (int i = 0; i < intentList.Count; i++)
            {
                var intent = WitIntent.FromJson(intentList[i]);
                intent.witConfiguration = this;
                intents[i] = intent;
                intent.Update();
            }
        }

        private void UpdateEntityList(JSONNode entityListJson)
        {
            var entityList = entityListJson.AsArray;
            entities = new WitEntity[entityList.Count];
            for (int i = 0; i < entityList.Count; i++)
            {
                var entity = WitEntity.FromJson(entityList[i]);
                entity.witConfiguration = this;
                entities[i] = entity;
                entity.Update();
            }
        }

        private void UpdateAppList(JSONNode appListJson)
        {
            var appList = appListJson.AsArray;
            applicationNames = new string[appList.Count];
            applications = new WitApplication[appList.Count];
            for (int i = 0; i < appList.Count; i++)
            {
                var app = WitApplication.FromJson(appList[i]);
                app.witConfiguration = this;
                applicationNames[i] = app.name;
                applications[i] = app;
            }
        }
    }
}
