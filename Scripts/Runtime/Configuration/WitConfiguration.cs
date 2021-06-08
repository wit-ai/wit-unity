/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.ComponentModel;
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
        [HideInInspector]
        [SerializeField] public WitApplication application;

        [ReadOnly(true)] [SerializeField] public string clientAccessToken;

        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;

        public WitApplication Application => application;

        public void UpdateData(Action onUpdateComplete = null)
        {
            var intentRequest = this.ListIntentsRequest();
            intentRequest.onResponse = (r) =>
            {
                var entityRequest = this.ListEntitiesRequest();
                entityRequest.onResponse =
                    (er) => OnUpdateData(er, UpdateEntityList, onUpdateComplete);
                OnUpdateData(r, UpdateIntentList, entityRequest.Request);
            };

            application?.UpdateData(intentRequest.Request);
        }

        private void OnUpdateData(WitRequest request, Action<WitResponseNode> updateComponent, Action onUpdateComplete)
        {
            if (request.StatusCode == 200)
            {
                updateComponent(request.ResponseData);
            }
            else
            {
                Debug.LogError($"Request for {request} failed: {request.StatusDescription}");
            }

            onUpdateComplete?.Invoke();
        }

        private void UpdateIntentList(WitResponseNode intentListWitResponse)
        {
            var intentList = intentListWitResponse.AsArray;
            intents = new WitIntent[intentList.Count];
            for (int i = 0; i < intentList.Count; i++)
            {
                var intent = WitIntent.FromJson(intentList[i]);
                intent.witConfiguration = this;
                intents[i] = intent;
                intent.UpdateData();
            }
        }

        private void UpdateEntityList(WitResponseNode entityListWitResponse)
        {
            var entityList = entityListWitResponse.AsArray;
            entities = new WitEntity[entityList.Count];
            for (int i = 0; i < entityList.Count; i++)
            {
                var entity = WitEntity.FromJson(entityList[i]);
                entity.witConfiguration = this;
                entities[i] = entity;
                entity.UpdateData();
            }
        }
    }
}
