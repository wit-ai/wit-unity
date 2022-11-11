/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Net;
using Meta.WitAi.Json;
using TMPro;
using UnityEngine;

namespace Meta.WitAi.Samples.ResponseDebugger
{
    public class WitUIInteractionHandler : MonoBehaviour
    {
        [Header("Wit")]
        [SerializeField] private Wit wit;
        [Header("UI")]
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI textArea;
        [Header("Configuration")]
        [SerializeField] private bool showJson;

        private string pendingText;

        private void OnValidate()
        {
            if (!wit) wit = FindObjectOfType<Wit>();
        }

        private void Update()
        {
            if (null != pendingText)
            {
                textArea.text = pendingText;
                pendingText = null;
            }
        }

        private void OnEnable()
        {
            wit.VoiceEvents.OnRequestCreated.AddListener(OnRequestStarted);
        }

        private void OnDisable()
        {
            wit.VoiceEvents.OnRequestCreated.RemoveListener(OnRequestStarted);
        }

        private void OnRequestStarted(WitRequest request)
        {
            // The raw response comes back on a different thread. We store the
            // message received for display on the next frame.
            if (showJson) request.onRawResponse += (response) => pendingText = response;
            request.onResponse += (r) =>
            {
                if (r.StatusCode == (int) HttpStatusCode.OK)
                {
                    OnResponse(r.ResponseData);
                }
                else
                {
                    OnError($"Error {r.StatusCode}", r.StatusDescription);
                }
            };
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

        public void Send()
        {
            textArea.text = $"Sending \"{inputField.text}\" to Wit.ai for processing...";
            wit.Activate(inputField.text);
        }

        public void LogResults(string[] parameters)
        {
            Debug.Log("Got the following entities back: " + string.Join(", ", parameters));
        }
    }
}
