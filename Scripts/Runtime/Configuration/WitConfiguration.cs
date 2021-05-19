/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.lib;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace com.facebook.witai.data
{
    [CreateAssetMenu(fileName = "WitConfiguration", menuName = "Wit/Configuration", order = 1)]
    public class WitConfiguration : ScriptableObject
    {
        [SerializeField] public string clientAccessToken;

        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;

        public string EditorToken
        {
#if UNITY_EDITOR
            get
            {
                return EditorPrefs.GetString("Wit::EditorToken", "");
            }
            set
            {
                EditorPrefs.SetString("Wit::EditorToken", value);
            }
#else
            get => clientAccessToken;
#endif
        }

        public void Update()
        {
            var request = this.ListIntentsRequest();
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
    }
}
