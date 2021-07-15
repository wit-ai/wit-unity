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
    public abstract class WitConfigurationData
    {
        [SerializeField] public WitConfiguration witConfiguration;

        #if UNITY_EDITOR
        public void UpdateData(Action onUpdateComplete = null)
        {
            var request = OnCreateRequest();
            request.onResponse = (r) => OnUpdateData(r, onUpdateComplete);
            request.Request();
        }

        protected abstract WitRequest OnCreateRequest();

        private void OnUpdateData(WitRequest request, Action onUpdateComplete)
        {
            if (request.StatusCode == 200)
            {
                UpdateData(request.ResponseData);
            }
            else
            {
                Debug.LogError(request.StatusDescription);
            }

            onUpdateComplete?.Invoke();
        }

        public abstract void UpdateData(WitResponseNode data);
        #endif
    }
}
