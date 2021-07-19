/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using com.facebook.witai.callbackhandlers;
using com.facebook.witai.Data;
using com.facebook.witai.lib;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai.utility
{
    public class WitUnderstandingViewer : BaseWitWindow
    {
        [SerializeField] private Texture2D witHeader;
        private string utterance;
        private bool loading;
        private WitResponseNode response;
        private Dictionary<string, bool> foldouts;

        [NonSerialized]
        private bool initSubmitted;

        [SerializeField]
        private bool submitted;

        private Vector2 scroll;
        private DateTime submitStart;
        private TimeSpan requestLength;
        private string status;
        private Wit wit;

        class Content
        {
            public static GUIContent copyPath;
            public static GUIContent copyCode;
            public static GUIContent createStringValue;
            public static GUIContent createIntValue;
            public static GUIContent createFloatValue;

            static Content()
            {
                createStringValue = new GUIContent("Create Value Reference/Create String");
                createIntValue = new GUIContent("Create Value Reference/Create Int");
                createFloatValue = new GUIContent("Create Value Reference/Create Float");

                copyPath = new GUIContent("Copy Path to Clipboard");
                copyCode = new GUIContent("Copy Code to Clipboard");
            }
        }

        #if !WIT_DISABLE_UI
        [MenuItem("Window/Wit/Understanding Viewer")]
        #endif
        static void Init()
        {
            WitUnderstandingViewer window = EditorWindow.GetWindow(typeof(WitUnderstandingViewer)) as WitUnderstandingViewer;
            window.titleContent = new GUIContent("Understanding Viewer", WitStyles.WitIcon);
            window.autoRepaintOnSceneChange = true;
            window.Show();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SetWit(GameObject.FindObjectOfType<Wit>());
        }

        protected override void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && !wit)
            {
                SetWit(FindObjectOfType<Wit>());
            }
        }

        private void OnSelectionChange()
        {
            if (Selection.activeGameObject)
            {
                wit = Selection.activeGameObject.GetComponent<Wit>();
                SetWit(wit);
            }
        }

        private void SetWit(Wit wit)
        {
            if (wit)
            {
                this.wit = wit;
                wit.events.OnRequestCreated.AddListener((r) =>
                {
                    submitStart = System.DateTime.Now;
                    loading = true;
                });
                wit.events.OnError.AddListener((title, message) =>
                {
                    status = message;
                    loading = false;
                });
                wit.events.OnResponse.AddListener(ShowResponse);
                status = $"Watching {wit.name} for responses.";
                Repaint();
            }
        }

        protected override void OnDrawContent()
        {
            if (!witConfiguration || witConfigs.Length > 1)
            {
                DrawWitConfigurationPopup();

                if (!witConfiguration)
                {
                    GUILayout.Label(
                        "A wit configuration must be available and selected to test utterances.", EditorStyles.helpBox);
                    return;
                }
            }

            GUILayout.BeginHorizontal();
            utterance = EditorGUILayout.TextField("Utterance", utterance);
            if (GUILayout.Button("Submit") && !loading || !initSubmitted && submitted && !string.IsNullOrEmpty(utterance))
            {
                if (!string.IsNullOrEmpty(utterance))
                {
                    submitted = true;
                    initSubmitted = true;
                    SubmitUtterance();
                }
                else
                {
                    loading = false;
                    response = null;
                }
            }
            GUILayout.EndHorizontal();

            if (loading)
            {
                BeginCenter();
                GUILayout.Label("Loading...");
                EndCenter();
            }
            else if (null != response)
            {
                GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.ExpandHeight(true));
                DrawResponse();
                GUILayout.EndVertical();
            }
            else
            {
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.Label(
                    "Enter an utterance and hit submit to see what your app will return.");
                GUILayout.EndVertical();
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(status, WitStyles.BackgroundBlack25P);
        }

        private void SubmitUtterance()
        {
            if (string.IsNullOrEmpty(utterance))
            {
                loading = false;
                return;
            }

            // Hack to watch for loading to complete. Response does not
            // come back on the main thread so Repaint in onResponse in
            // the editor does nothing.
            EditorApplication.update += WatchForResponse;

            if (Application.isPlaying && !wit)
            {
                SetDefaultWit();
            }

            if (wit && Application.isPlaying)
            {
                wit.Activate(utterance);
            }
            else
            {
                submitStart = System.DateTime.Now;
                var request = witConfiguration.MessageRequest(utterance);
                request.onResponse = (r) => ShowResponse(r.ResponseData);
                request.Request();
                loading = true;
            }
        }

        private void SetDefaultWit()
        {
            SetWit(FindObjectOfType<Wit>());
        }

        private void ShowResponse(WitResponseNode r)
        {
            requestLength = DateTime.Now - submitStart;
            response = r;
            loading = false;
            status = $"Response time: {requestLength}";
            EditorForegroundRunner.Run(Repaint);
        }

        private void WatchForResponse()
        {
            if (loading == false)
            {
                Repaint();
                EditorApplication.update -= WatchForResponse;
            }
        }

        private void DrawResponse()
        {
            scroll = GUILayout.BeginScrollView(scroll);
            DrawResponseNode(response);
            GUILayout.EndScrollView();
        }

        private void DrawResponseNode(WitResponseNode witResponseNode, string path = "")
        {
            if (null == witResponseNode?.AsObject) return;

            foreach (var child in witResponseNode.AsObject.ChildNodeNames)
            {
                var childNode = witResponseNode[child];
                DrawNode(childNode, child, path);
            }
        }

        private void DrawNode(WitResponseNode childNode, string child, string path, bool isArrayElement = false)
        {
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
            menu.AddItem(Content.createStringValue, false, () => WitDataCreation.CreateStringValue(path));
            menu.AddItem(Content.createIntValue, false, () => WitDataCreation.CreateIntValue(path));
            menu.AddItem(Content.createFloatValue, false, () => WitDataCreation.CreateFloatValue(path));
            menu.AddSeparator("");
            menu.AddItem(Content.copyPath, false, () =>
            {
                EditorGUIUtility.systemCopyBuffer = path;
            });
            menu.AddItem(Content.copyCode, false, () =>
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
                    valueHandler.intent = response.GetIntentName();
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
            if (null == foldouts) foldouts = new Dictionary<string, bool>();
            if (!foldouts.TryGetValue(path, out var state))
            {
                state = false;
                foldouts[path] = state;
            }

            var newState = EditorGUILayout.Foldout(state, label);
            if (newState != state)
            {
                foldouts[path] = newState;
            }

            return newState;
        }
    }
}
