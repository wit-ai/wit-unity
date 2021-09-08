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
    #if !WIT_DISABLE_UI
    [CreateAssetMenu(fileName = "WitConfiguration", menuName = "Wit/Configuration", order = 1)]
    #endif
    public class WitConfiguration : ScriptableObject
    {
        [HideInInspector]
        [SerializeField] public WitApplication application;

        [ReadOnly(true)] [SerializeField] public string clientAccessToken;

        [SerializeField] public WitEntity[] entities;
        [SerializeField] public WitIntent[] intents;
        [SerializeField] public WitTrait[] traits;

        public WitApplication Application => application;

        #if UNITY_EDITOR
        public void UpdateData(Action onUpdateComplete = null)
        {
            if (!string.IsNullOrEmpty(WitAuthUtility.AppServerToken))
            {
                var intentsRequest = this.ListIntentsRequest();
                intentsRequest.onResponse = (r) =>
                {
                    var entitiesRequest = this.ListEntitiesRequest();
                    entitiesRequest.onResponse = (er) =>
                    {
                        var traitsRequest = this.ListTraitsRequest();
                        traitsRequest.onResponse =
                          (tr) => OnUpdateData(tr, UpdateTraitList, onUpdateComplete);
                        OnUpdateData(er, UpdateEntityList, traitsRequest.Request);
                    };
                    OnUpdateData(r, UpdateIntentList, entitiesRequest.Request);
                };

                application?.UpdateData(intentsRequest.Request);
            }
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
            var n = intentList.Count;
            intents = new WitIntent[n];
            for (int i = 0; i < n; i++)
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
            var n = entityList.Count;
            entities = new WitEntity[n];
            for (int i = 0; i < n; i++)
            {
                var entity = WitEntity.FromJson(entityList[i]);
                entity.witConfiguration = this;
                entities[i] = entity;
                entity.UpdateData();
            }
        }

        private void UpdateTraitList(WitResponseNode traitListWitResponse)
        {
            var traitList = traitListWitResponse.AsArray;
            var n = traitList.Count;
            traits = new WitTrait[n];
            for (int i = 0; i < n; i++) {
                var trait = WitTrait.FromJson(traitList[i]);
                trait.witConfiguration = this;
                traits[i] = trait;
                trait.UpdateData();
            }
        }

        /// <summary>
        /// Gets the app info and client id that is associated with the server token being used
        /// </summary>
        /// <param name="action"></param>
        public void FetchAppConfigFromServerToken(Action action)
        {
            FetchApplicationFromServerToken(() =>
            {
                FetchClientToken(() =>
                {
                    UpdateData(action);
                });
            });
        }

        private void FetchApplicationFromServerToken(Action response)
        {
            var listRequest = this.ListAppsRequest(10000);
            listRequest.onResponse = (r) =>
            {
                if (r.StatusCode == 200)
                {
                    var applications = r.ResponseData.AsArray;
                    for (int i = 0; i < applications.Count; i++)
                    {
                        if (applications[i]["is_app_for_token"].AsBool)
                        {
                            if (null != application)
                            {
                                application.UpdateData(applications[i]);
                            }
                            else
                            {
                                application = WitApplication.FromJson(applications[i]);
                            }

                            break;
                        }
                    }

                    response?.Invoke();
                }
                else
                {
                    Debug.LogError(r.StatusDescription);
                }
            };
            listRequest.Request();
        }

        private void FetchClientToken(Action action)
        {
            if (!string.IsNullOrEmpty(application?.id))
            {
                var tokenRequest = this.GetClientToken(application.id);
                tokenRequest.onResponse = (r) =>
                {
                    if (r.StatusCode == 200)
                    {
                        clientAccessToken = r.ResponseData["client_token"];
                        action?.Invoke();
                    }
                    else
                    {
                        Debug.LogError(r.StatusDescription);
                    }
                };
                tokenRequest.Request();
            }
        }
#endif
    }
}
