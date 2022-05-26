/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi.CallbackHandlers
{
    public abstract class WitResponseHandler : MonoBehaviour
    {
        [SerializeField] public VoiceService wit;

        /// <summary>
        /// Whether or not to handle partial responses the same as final responses
        /// </summary>
        [SerializeField] public bool handlePartialResponses = false;

        private void OnValidate()
        {
            if (!wit) wit = FindObjectOfType<VoiceService>();
        }

        private void OnEnable()
        {
            if (!wit) wit = FindObjectOfType<VoiceService>();
            if (!wit)
            {
                Debug.LogError("Wit not found in scene. Disabling " + GetType().Name + " on " +
                               name);
                enabled = false;
            }
            else
            {
                wit.events.OnPartialResponse.AddListener(OnHandlePartialResponse);
                wit.events.OnResponse.AddListener(OnHandleResponse);
            }
        }

        private void OnDisable()
        {
            if (wit)
            {
                wit.events.OnPartialResponse.RemoveListener(OnHandlePartialResponse);
                wit.events.OnResponse.RemoveListener(OnHandleResponse);
            }
        }

        public void HandleResponse(WitResponseNode response) => OnHandleResponse(response);
        protected abstract void OnHandleResponse(WitResponseNode response);

        public void HandlePartialResponse(WitResponseNode response) => OnHandlePartialResponse(response);
        protected virtual void OnHandlePartialResponse(WitResponseNode response)
        {
            if (handlePartialResponses)
            {
                OnHandleResponse(response);
            }
        }
    }
}
