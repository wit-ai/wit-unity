/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Facebook.WitAi.CallbackHandlers;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Lib;
using Facebook.WitAi.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

namespace Facebook.WitAi.Windows
{
    public class WitUnderstandingViewer : WitConfigurationWindow
    {
        [FormerlySerializedAs("witHeader")] [SerializeField] private Texture2D _witHeader;
        [FormerlySerializedAs("responseText")] [SerializeField] private string _responseText;
        private string _utterance;
        private WitResponseNode _response;
        private Dictionary<string, bool> _foldouts;

        private DateTime _submitStart;
        private TimeSpan _requestLength;
        private string _status;
        private VoiceService _wit;
        private int _responseCode;
        private WitRequest _request;
        private int _savePopup;
        private GUIStyle _hamburgerButton;

        public bool HasWit => null != _wit;

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
            SetWit(GameObject.FindObjectOfType<VoiceService>());
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
                SetWit(FindObjectOfType<VoiceService>());
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject)
            {
                _wit = Selection.activeGameObject.GetComponent<VoiceService>();
                SetWit(_wit);
            }
        }

        private void SetWit(VoiceService wit)
        {
            if (HasWit)
            {
                wit.events.OnRequestCreated.RemoveListener(OnRequestCreated);
                wit.events.OnError.RemoveListener(OnError);
                wit.events.OnResponse.RemoveListener(ShowResponse);
                wit.events.OnFullTranscription.RemoveListener(ShowTranscription);
                wit.events.OnPartialTranscription.RemoveListener(ShowTranscription);
            }
            if (null != wit)
            {
                this._wit = wit;
                wit.events.OnRequestCreated.AddListener(OnRequestCreated);
                wit.events.OnError.AddListener(OnError);
                wit.events.OnResponse.AddListener(ShowResponse);
                wit.events.OnFullTranscription.AddListener(ShowTranscription);
                wit.events.OnPartialTranscription.AddListener(ShowTranscription);
                // We will be measuring perceived request time since the actual request starts
                // as soon as the mic goes active and the user says something.
                wit.events.OnStoppedListening.AddListener(ResetStartTime);
                Repaint();
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
            // Layout wit select
            base.LayoutContent();

            // Need configuration
            if (!witConfiguration)
            {
                WitEditorUI.LayoutErrorLabel(WitTexts.Texts.UnderstandingViewerMissingConfigLabel);
                return;
            }
            // Need app id
            string clientAccessToken = witConfiguration.clientAccessToken;
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
            bool updated = false;
            bool allowInput = !_wit || !_wit.Active;
            GUI.enabled = allowInput;
            WitEditorUI.LayoutTextField(new GUIContent(WitTexts.Texts.UnderstandingViewerUtteranceLabel), ref _utterance, ref updated);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
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
            GUI.enabled = true;

            if (EditorApplication.isPlaying && _wit)
            {
                if (!_wit.Active && WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerActivateButtonLabel))
                {
                    _wit.Activate();
                }

                if (_wit.Active && WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerDeactivateButtonLabel))
                {
                    _wit.Deactivate();
                }

                if (_wit.Active && WitEditorUI.LayoutTextButton(WitTexts.Texts.UnderstandingViewerAbortButtonLabel))
                {
                    _wit.DeactivateAndAbortRequest();
                }
            }
            GUILayout.EndHorizontal();

            // Results
            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (_wit && _wit.MicActive)
            {
                WitEditorUI.LayoutWrapLabel(WitTexts.Texts.UnderstandingViewerListeningLabel);
            }
            else if (_wit && _wit.IsRequestActive)
            {
                WitEditorUI.LayoutWrapLabel(WitTexts.Texts.UnderstandingViewerLoadingLabel);
            }
            else if (_response != null)
            {
                DrawResponse();
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
            if (Application.isPlaying && !HasWit)
            {
                SetDefaultWit();
            }

            // Remove response
            _response = null;

            if (_wit && Application.isPlaying)
            {
                _status = WitTexts.Texts.UnderstandingViewerListeningLabel;
                _responseText = _status;
                _wit.Activate(_utterance);
                // Hack to watch for loading to complete. Response does not
                // come back on the main thread so Repaint in onResponse in
                // the editor does nothing.
                EditorApplication.update += WatchForWitResponse;
            }
            else
            {
                _status = WitTexts.Texts.UnderstandingViewerLoadingLabel;
                _responseText = _status;
                _submitStart = System.DateTime.Now;
                _request = witConfiguration.MessageRequest(_utterance, new WitRequestOptions());
                _request.onResponse = OnResponse;
                _request.Request();
            }
        }

        private void WatchForWitResponse()
        {
            if (_wit && !_wit.Active)
            {
                Repaint();
                EditorApplication.update -= WatchForWitResponse;
            }
        }

        private void SetDefaultWit()
        {
            SetWit(FindObjectOfType<VoiceService>());
        }

        private void OnResponse(WitRequest request)
        {
            _responseCode = request.StatusCode;
            if (null != request.ResponseData)
            {
                ShowResponse(request.ResponseData);
            }
            else if (!string.IsNullOrEmpty(request.StatusDescription))
            {
                _responseText = request.StatusDescription;
            }
            else
            {
                _responseText = "No response. Status: " + request.StatusCode;
            }
        }

        private void ShowResponse(WitResponseNode r)
        {
            _response = r;
            _responseText = _response.ToString();
            _requestLength = DateTime.Now - _submitStart;
            _status = $"Response time: {_requestLength}";
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
                if (GUILayout.Button($"{child} = {childNode.Value}", "Label"))
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
    }
}
