/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using TMPro;
using UnityEngine;

namespace Meta.Voice.Samples.WitResponseDebugger
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
            wit.VoiceEvents.OnSend.AddListener(OnSend);
            wit.VoiceEvents.OnComplete.AddListener(OnComplete);
        }

        private void OnDisable()
        {
            wit.VoiceEvents.OnSend.RemoveListener(OnSend);
            wit.VoiceEvents.OnComplete.AddListener(OnComplete);
        }

        private void OnSend(VoiceServiceRequest request)
        {
            // The raw response comes back on a different thread. We store the
            // message received for display on the next frame.
            if (showJson && request is WitRequest witRequest)
            {
                witRequest.Events.OnRawResponse.AddListener((response) => pendingText = response);
            }
        }

        private void OnComplete(VoiceServiceRequest request)
        {
            if (request.State == VoiceRequestState.Successful)
            {
                OnResponse(request.Results.ResponseData);
            }
            else if (request.State == VoiceRequestState.Failed)
            {
                OnError($"Error {request.Results.StatusCode}", request.Results.Message);
            }
            else if (request.State == VoiceRequestState.Canceled)
            {
                textArea.text = request.Results.Message;
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
