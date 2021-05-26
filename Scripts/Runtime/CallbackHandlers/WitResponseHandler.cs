﻿/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai.callbackhandlers
{
    public abstract class WitResponseHandler : MonoBehaviour
    {
        [SerializeField] public Wit wit;

        private void OnValidate()
        {
            if (!wit) wit = FindObjectOfType<Wit>();
        }

        private void OnEnable()
        {
            if (!wit) wit = FindObjectOfType<Wit>();
            if (!wit)
            {
                Debug.LogError("Wit not found in scene. Disabling " + GetType().Name + " on " +
                               name);
                enabled = false;
            }
            else
            {
                wit.events.OnResponse.AddListener(OnHandleResponse);
            }
        }

        private void OnDisable()
        {
            wit.events.OnResponse.RemoveListener(OnHandleResponse);
        }

        protected abstract void OnHandleResponse(WitResponseNode response);
    }
}
