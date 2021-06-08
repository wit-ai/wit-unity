/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;
using TMPro;
using UnityEngine;

namespace com.facebook.witai.samples.shapes
{
    public class WitUIInteractionHandler : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI textArea;
        [SerializeField] private Wit wit;
        [SerializeField] private bool showJson;

        private string pendingText;

        private void OnEnable()
        {
            wit.onRequestStarted += OnRequestStarted;
        }

        private void OnDisable()
        {
            wit.onRequestStarted -= OnRequestStarted;
        }

        private void OnRequestStarted(WitRequest r)
        {
            // The raw response comes back on a different thread. We store the
            // message received for display on the next frame.
            if (showJson) r.onRawResponse = (response) => pendingText = response;
        }

        private void Update()
        {
            if (null != pendingText)
            {
                textArea.text = pendingText;
                pendingText = null;
            }
        }

        public void OnResponse(WitResponseNode response)
        {
            if (!showJson) textArea.text = response["text"];
        }

        public void OnError(string error, string message)
        {
            textArea.text = $"Error: {error}\n\n{message}";
        }

        public void ToggleActivation()
        {
            if (wit.Active) wit.Deactivate();
            else
            {
                textArea.text = "The mic is active, start speaking now.";
                wit.Activate();
            }
        }
    }
}
