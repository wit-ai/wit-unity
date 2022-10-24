/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Facebook.WitAi.Windows;
using Meta.WitAi.Dictation;
using Meta.WitAi.CallbackHandlers;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Meta.WitAi.Windows
{
    public class WitUnderstandingViewer : WitConfigurationWindow
    {
        [FormerlySerializedAs("witHeader")] [SerializeField] private Texture2D _witHeader;
        [FormerlySerializedAs("responseText")] [SerializeField] private string _responseText;
        private string _utterance;
        private WitResponseNode _response;
        private Dictionary<string, bool> _foldouts;

        // Current service
        private WitUnderstandingViewerServiceAPI[] _services;
        private string[] _serviceNames;
        private int _currentService = -1;
        public WitUnderstandingViewerServiceAPI service =>
            _services != null
            && _currentService >= 0
            && _currentService < _services.Length ? _services[_currentService] : null;
        public bool HasWit => service != null;

        private DateTime _submitStart;
        private TimeSpan _requestLength;
        private string _status;
        private int _responseCode;
        private WitRequest _request;
        private int _savePopup;
        private GUIStyle _hamburgerButton;
        private Vector2 _utteranceScrollPosition;

        class Content
        {
            public static GUIContent CopyPath;
            public static GUIContent CopyCode;
            public static GUIContent CreateStringValue;
            public static GUIContent CreateIntValue;
            public static GUIContent CreateFloatValue;

            static Content()
            {
                CreateStringValue = new GUIContent("Create Value Reference/Create String");
                CreateIntValue = new GUIContent("Create Value Reference/Create Int");
                CreateFloatValue = new GUIContent("Create Value Reference/Create Float");

                CopyPath = new GUIContent("Copy Path to Clipboard");
                CopyCode = new GUIContent("Copy Code to Clipboard");
            }
        }

        protected override GUIContent Title => WitTexts.UnderstandingTitleContent;
        protected override WitTexts.WitAppEndpointType HeaderEndpointType => WitTexts.WitAppEndpointType.Understanding;

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            RefreshServices();

            if (!string.IsNullOrEmpty(_responseText))
            {
                _response = WitResponseNode.Parse(_responseText);
            }
            _status = WitTexts.Texts.UnderstandingViewerPromptLabel;
        }

        protected override void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && !HasWit)
            {
                RefreshServices();
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject)
            {
                SetService(Selection.activeGameObject);
            }
        }

        private void ResetStartTime()
        {
            _submitStart = System.DateTime.Now;
        }

        private void OnError(string title, string message)
        {
            _status = message;
        }

        private void OnRequestCreated(WitRequest request)
        {
            this._request = request;
            ResetStartTime();
        }

        private void ShowTranscription(string transcription)
        {
            _utterance = transcription;
            Repaint();
        }

        // On gui
        protected override void OnGUI()
        {
            base.OnGUI();
            EditorGUILayout.BeginHorizontal();
            WitEditorUI.LayoutStatusLabel(_status);
            GUILayout.BeginVertical(GUILayout.Width(24));
            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            GUILayout.Space(4);
            var rect = GUILayoutUtility.GetLastRect();

            if (null == _hamburgerButton)
            {
                // GUI.skin must be called from OnGUI
                _hamburgerButton = new GUIStyle(GUI.skin.GetStyle("PaneOptions"));
                _hamburgerButton.imagePosition = ImagePosition.ImageOnly;
            }

            var value = EditorGUILayout.Popup(-1, new string[] {"Save", "Copy to Clipboard"}, _hamburgerButton, GUILayout.Width(24));
            if (-1 != value)
            {
                if (value == 0)
                {
                    var path = EditorUtility.SaveFilePanel("Save Response Json", Application.dataPath,
                        "result", "json");
                    if (!string.IsNullOrEmpty(path))
                    {
                        File.WriteAllText(path, _response.ToString());
                    }
                }
                else
                {
                    EditorGUIUtility.systemCopyBuffer = _response.ToString();
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        protected override void LayoutContent()
        {
            // Get service
            WitUnderstandingViewerServiceAPI voiceService = null;

            // Runtime Mode
            if (Application.isPlaying)
            {
                // Refresh services
                if (_services == null)
                {
                    RefreshServices();
                }
                // Services missing
                if (_services == null || _serviceNames == null || _services.Length == 0)
                {
                    WitEditorUI.LayoutErrorLabel(WitTexts.Texts.UnderstandingViewerMissingServicesLabel);
                    return;
                }
                // Voice service select
                int newService = _currentService;
                bool serviceUpdate = false;
                GUILayout.BeginHorizontal();
                // Clamp
                if (newService < 0 || newService >= _services.Length)
                {
                    newService = 0;
                    serviceUpdate = true;
                }
                // Layout
                GUILayout.Space(3);

                WitEditorUI.LayoutPopup(WitTexts.Texts.UnderstandingViewerServicesLabel, _serviceNames, ref newService, ref serviceUpdate);
                // Update
                if (serviceUpdate)
                {
                    SetService(newService);
                }

                // Select
                bool selectPressed = GUILayout.Button("", GUI.skin.GetStyle("IN ObjectField"), GUILayout.Width(15), GUILayout.Height(20), GUILayout.ExpandWidth(false));

                if (_currentService >= 0 && _currentService < _services.Length && selectPressed)
                {
                    if (_services != null && _services.Length > 0 && _services[0].ServiceComponent == null)
                    {
                        RefreshServices();
                    }

                    Selection.activeObject = _services[_currentService].ServiceComponent.gameObject;
                }

                // Refresh
                if (WitEditorUI.LayoutIconButton(EditorGUIUtility.IconContent("d_Refresh")))
                {
                    RefreshServices();
                }
                GUILayout.EndHorizontal();
                // Ensure service exists
                voiceService = service;
            }
            // Editor Only
            else
            {
                // Configuration select
                base.LayoutContent();
                // Ensure configuration exists
                if (!witConfiguration)
                {
                    WitEditorUI.LayoutErrorLabel(WitTexts.Texts.UnderstandingViewerMissingConfigLabel);
                    return;
                }
                // Check client access token
                string clientAccessToken = witConfiguration.GetClientAccessToken();
                if (string.IsNullOrEmpty(clientAccessToken))
                {
                    WitEditorUI.LayoutErrorLabel(WitTexts.Texts.UnderstandingViewerMissingClientTokenLabel);
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerSettingsButtonLabel))
                    {
                        Selection.activeObject = witConfiguration;
                    }
                    GUILayout.EndHorizontal();
                    return;
                }
            }

            // Determine if input is allowed
            bool allowInput = !Application.isPlaying || (service != null && !service.Active);
            GUI.enabled = allowInput;

            // Utterance field - if selected service is a Voice Service then the field is enabled for input.
            if (_currentService != -1 && _services[_currentService] is WitUnderstandingViewerVoiceServiceAPI)
            {
                bool updated = false;
                WitEditorUI.LayoutTextField(new GUIContent(WitTexts.Texts.UnderstandingViewerUtteranceLabel), ref _utterance, ref updated);
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(WitTexts.Texts.UnderstandingViewerUtteranceLabel, WitStyles.Label, GUILayout.Width(140));
                _utteranceScrollPosition = EditorGUILayout.BeginScrollView(_utteranceScrollPosition, GUILayout.Height(EditorGUIUtility.singleLineHeight * 2));
                WitEditorUI.LayoutWrapLabel(_utterance);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndHorizontal();
            }

            // Begin Buttons
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            // Submit utterance (Voice Service only)
            if (_currentService != -1 && _services[_currentService] is WitUnderstandingViewerVoiceServiceAPI)
            {
                if (allowInput && WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerSubmitButtonLabel))
                {
                    _responseText = "";
                    if (!string.IsNullOrEmpty(_utterance))
                    {
                        SubmitUtterance();
                    }
                    else
                    {
                        _response = null;
                    }
                }
            }

            // Service buttons
            GUI.enabled = true;
            if (EditorApplication.isPlaying && voiceService != null)
            {
                if (!voiceService.Active)
                {
                    // Activate
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerActivateButtonLabel))
                    {
                        voiceService.Activate();
                    }
                }
                else
                {
                    // Deactivate
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerDeactivateButtonLabel))
                    {
                        voiceService.Deactivate();
                    }
                    // Abort
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerAbortButtonLabel))
                    {
                        voiceService.DeactivateAndAbortRequest();
                    }
                }
            }
            GUILayout.EndHorizontal();

            // Results
            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (_response != null)
            {
                DrawResponse();
            }
            else if (voiceService != null && voiceService.MicActive)
            {
                WitEditorUI.LayoutWrapLabel(WitTexts.Texts.UnderstandingViewerListeningLabel);
            }
            else if (voiceService != null && voiceService.IsRequestActive)
            {
                WitEditorUI.LayoutWrapLabel(WitTexts.Texts.UnderstandingViewerLoadingLabel);
            }
            else if (string.IsNullOrEmpty(_responseText))
            {
                WitEditorUI.LayoutWrapLabel(WitTexts.Texts.UnderstandingViewerPromptLabel);
            }
            else
            {
                WitEditorUI.LayoutWrapLabel(_responseText);
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
        }

        private void SubmitUtterance()
        {
            // Remove response
            _response = null;

            if (Application.isPlaying)
            {
                if (service != null)
                {
                    _status = WitTexts.Texts.UnderstandingViewerListeningLabel;
                    _responseText = _status;
                    service.Activate(_utterance);
                    // Hack to watch for loading to complete. Response does not
                    // come back on the main thread so Repaint in onResponse in
                    // the editor does nothing.
                    EditorApplication.update += WatchForWitResponse;
                }
            }
            else
            {
                _status = WitTexts.Texts.UnderstandingViewerLoadingLabel;
                _responseText = _status;
                _submitStart = System.DateTime.Now;
                _request = witConfiguration.CreateMessageRequest(_utterance, new WitRequestOptions());
                _request.onResponse += (r) => OnResponse(r?.ResponseData);
                _request.Request();
            }
        }

        private void WatchForWitResponse()
        {
            if (service != null && !service.Active)
            {
                Repaint();
                EditorApplication.update -= WatchForWitResponse;
            }
        }

        private void OnResponse(WitResponseNode ResponseData)
        {
            _responseCode = _request.StatusCode;
            if (null != ResponseData)
            {
                ShowResponse(ResponseData, false);
            }
            else if (!string.IsNullOrEmpty(_request.StatusDescription))
            {
                _responseText = _request.StatusDescription;
            }
            else
            {
                _responseText = "No response. Status: " + _request.StatusCode;
            }
        }

        private void ShowResponse(WitResponseNode r, bool isPartial)
        {
            _response = r;
            _responseText = _response.ToString();
            _requestLength = DateTime.Now - _submitStart;
            _status = $"{(isPartial ? "Partial" : "Full")}Response time: {_requestLength}";
        }

        private void DrawResponse()
        {
            DrawResponseNode(_response);
        }

        private void DrawResponseNode(WitResponseNode witResponseNode, string path = "")
        {
            if (null == witResponseNode?.AsObject) return;

            if(string.IsNullOrEmpty(path)) DrawNode(witResponseNode["text"], "text", path);

            var names = witResponseNode.AsObject.ChildNodeNames;
            Array.Sort(names);
            foreach (string child in names)
            {
                if (!(string.IsNullOrEmpty(path) && child == "text"))
                {
                    var childNode = witResponseNode[child];
                    DrawNode(childNode, child, path);
                }
            }
        }

        private void DrawNode(WitResponseNode childNode, string child, string path, bool isArrayElement = false)
        {
            if (childNode == null)
            {
                return;
            }
            string childPath;

            if (path.Length > 0)
            {
                childPath = isArrayElement ? $"{path}[{child}]" : $"{path}.{child}";
            }
            else
            {
                childPath = child;
            }

            if (!string.IsNullOrEmpty(childNode.Value))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(15 * EditorGUI.indentLevel);
                if (GUILayout.Button($"{child} = {childNode.Value}", WitStyles.LabelWrap))
                {
                    ShowNodeMenu(childNode, childPath);
                }

                GUILayout.EndHorizontal();
            }
            else
            {
                var childObject = childNode.AsObject;
                var childArray = childNode.AsArray;

                if ((null != childObject || null != childArray) && Foldout(childPath, child))
                {
                    EditorGUI.indentLevel++;
                    if (null != childObject)
                    {
                        DrawResponseNode(childNode, childPath);
                    }

                    if (null != childArray)
                    {
                        DrawArray(childArray, childPath);
                    }

                    EditorGUI.indentLevel--;
                }
            }
        }

        private void ShowNodeMenu(WitResponseNode node, string path)
        {
            GenericMenu menu = new GenericMenu();
            menu.AddItem(Content.CreateStringValue, false, () => WitDataCreation.CreateStringValue(path));
            menu.AddItem(Content.CreateIntValue, false, () => WitDataCreation.CreateIntValue(path));
            menu.AddItem(Content.CreateFloatValue, false, () => WitDataCreation.CreateFloatValue(path));
            menu.AddSeparator("");
            menu.AddItem(Content.CopyPath, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = path;
            });
            menu.AddItem(Content.CopyCode, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = WitResultUtilities.GetCodeFromPath(path);
            });

            if (Selection.activeGameObject)
            {
                menu.AddSeparator("");

                var label =
                    new GUIContent($"Add response matcher to {Selection.activeObject.name}");

                menu.AddItem(label, false, () =>
                {
                    var valueHandler = Selection.activeGameObject.AddComponent<WitResponseMatcher>();
                    valueHandler.intent = _response.GetIntentName();
                    valueHandler.valueMatchers = new ValuePathMatcher[]
                    {
                        new ValuePathMatcher() { path = path }
                    };
                });

                AddMultiValueUpdateItems(path, menu);
            }

            menu.ShowAsContext();
        }

        private void AddMultiValueUpdateItems(string path, GenericMenu menu)
        {

            string name = path;
            int index = path.LastIndexOf('.');
            if (index > 0)
            {
                name = name.Substring(index + 1);
            }

            var mvhs = Selection.activeGameObject.GetComponents<WitResponseMatcher>();
            if (mvhs.Length > 1)
            {
                for (int i = 0; i < mvhs.Length; i++)
                {
                    var handler = mvhs[i];
                    menu.AddItem(
                        new GUIContent($"Add {name} matcher to {Selection.activeGameObject.name}/Handler {(i + 1)}"),
                        false, (h) => AddNewEventHandlerPath((WitResponseMatcher) h, path), handler);
                }
            }
            else if (mvhs.Length == 1)
            {
                var handler = mvhs[0];
                menu.AddItem(
                    new GUIContent($"Add {name} matcher to {Selection.activeGameObject.name}'s Response Matcher"),
                    false, (h) => AddNewEventHandlerPath((WitResponseMatcher) h, path), handler);
            }
        }

        private void AddNewEventHandlerPath(WitResponseMatcher handler, string path)
        {
            Array.Resize(ref handler.valueMatchers, handler.valueMatchers.Length + 1);
            handler.valueMatchers[handler.valueMatchers.Length - 1] = new ValuePathMatcher()
            {
                path = path
            };
        }

        private void DrawArray(WitResponseArray childArray, string childPath)
        {
            for (int i = 0; i < childArray.Count; i++)
            {
                DrawNode(childArray[i], i.ToString(), childPath, true);
            }
        }

        private bool Foldout(string path, string label)
        {
            if (null == _foldouts) _foldouts = new Dictionary<string, bool>();
            if (!_foldouts.TryGetValue(path, out var state))
            {
                state = false;
                _foldouts[path] = state;
            }

            var newState = EditorGUILayout.Foldout(state, label);
            if (newState != state)
            {
                _foldouts[path] = newState;
            }

            return newState;
        }

        #region SERVICES

        protected void RefreshServices()
        {
            // Remove previous service
            WitUnderstandingViewerServiceAPI previous = service;
            SetService(-1);

            List<WitUnderstandingViewerServiceAPI> services = new List<WitUnderstandingViewerServiceAPI>();
            List<string> serviceNames = new List<string>();

            // Get all supported services
            List<VoiceService> voiceServiceComponents = new List<VoiceService>(FindObjectsOfType<VoiceService>());
            List<DictationService> dictationServiceComponents = new List<DictationService>(FindObjectsOfType<DictationService>());

            HashSet<GameObject> uniqueGameObjects = new HashSet<GameObject>();

            // Get unique services
            foreach (var voiceServiceComponent in voiceServiceComponents)
            {
                if (uniqueGameObjects.Add(voiceServiceComponent.gameObject))
                {
                    services.Add(new WitUnderstandingViewerVoiceServiceAPI(voiceServiceComponent));
                }
            }

            foreach (var dictationServiceComponent in dictationServiceComponents)
            {
                if (uniqueGameObjects.Add(dictationServiceComponent.gameObject))
                {
                    services.Add(new WitUnderstandingViewerDictationServiceAPI(dictationServiceComponent));
                }
            }

            foreach (var service in services)
            {
                serviceNames.Add(service.ServiceName);
            }

            _services = services.ToArray();
            _serviceNames = serviceNames.ToArray();

            // Set as first found
            if (previous == null)
            {
                SetService(0);
            }
            else
            {
                // Set as previous
                SetService(previous);
            }
        }

        // Set voice service
        protected void SetService(WitUnderstandingViewerServiceAPI newService)
        {
            // Cannot set without services
            if (_services == null)
            {
                return;
            }

            // Check for lost references - if the UnderstandingViewer is docked for some reason
            // the GameObjects can get decoupled from the service APIs
            if (_services.Length != 0 && _services[0].ServiceComponent == null)
            {
                RefreshServices();
            }

            // Find & apply
            int newServiceIndex = Array.FindIndex(_services, (s) => s.ServiceName == newService.ServiceName);

            // Apply
            SetService(newServiceIndex);
        }

        protected void SetService(GameObject newService)
        {
            // Cannot set without services
            if (_services == null)
            {
                return;
            }

            // Check for lost references - if the UnderstandingViewer is docked for some reason
            // the GameObjects can get decoupled from the service APIs
            if (_services.Length != 0 && _services[0].ServiceComponent == null)
            {
                RefreshServices();
            }

            // Find & apply
            int newServiceIndex = Array.FindIndex(_services, (s) => s.ServiceComponent.gameObject == newService);

            // Apply
            SetService(newServiceIndex);
        }

        // Set
        protected void SetService(int newServiceIndex)
        {
            // Cannot set without services
            if (_services == null)
            {
                return;
            }

            // Remove listeners to current service
            RemoveListeners(service);

            // Set current index.
            _currentService = Mathf.Max(0, newServiceIndex);

            // Add listeners to current service
            AddListeners(service);
        }
        // Remove listeners
        private void RemoveListeners(WitUnderstandingViewerServiceAPI serviceAPI)
        {
            // Ignore
            if (serviceAPI == null)
            {
                return;
            }

            // Remove delegates
            serviceAPI.OnRequestCreated?.RemoveListener(OnRequestCreated);
            serviceAPI.OnError?.RemoveListener(OnError);
            serviceAPI.OnResponse?.RemoveListener(OnResponse);
            serviceAPI.OnFullTranscription?.RemoveListener(ShowTranscription);
            serviceAPI.OnPartialTranscription?.RemoveListener(ShowTranscription);
            serviceAPI.OnStoppedListening?.RemoveListener(ResetStartTime);
        }
        // Add listeners
        private void AddListeners(WitUnderstandingViewerServiceAPI serviceAPI)
        {
            // Ignore
            if (serviceAPI == null)
            {
                return;
            }

            // Add delegates
            serviceAPI.OnRequestCreated?.AddListener(OnRequestCreated);
            serviceAPI.OnError?.AddListener(OnError);
            serviceAPI.OnResponse?.AddListener(OnResponse);
            serviceAPI.OnPartialTranscription?.AddListener(ShowTranscription);
            serviceAPI.OnFullTranscription?.AddListener(ShowTranscription);
            serviceAPI.OnStoppedListening?.AddListener(ResetStartTime);
        }
        #endregion
    }
}
