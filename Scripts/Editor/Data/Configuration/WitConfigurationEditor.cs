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
using System.Linq;
using System.Text;
using Lib.Wit.Runtime.Requests;
using Meta.Conduit.Editor;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Utilities;
using Meta.Conduit;
using Meta.WitAi;
using UnityEditor;
using UnityEngine;
using Meta.WitAi.Windows.Conponents;

namespace Meta.WitAi.Windows
{
    public class WitConfigurationEditor : Editor
    {
        public WitConfiguration configuration { get; private set; }
        private string _serverToken;
        private string _appName;
        private string _appID;
        private bool _initialized = false;
        public bool drawHeader = true;
        private bool _foldout = true;
        private int _requestTab = 0;
        private bool manifestAvailable = false;
        private bool syncInProgress = false;
        private bool _didCheckAutoTrainAvailability = false;
        private bool _isAutoTrainAvailable = false;

        private static ConduitStatistics _statistics;
        private static readonly AssemblyMiner AssemblyMiner = new AssemblyMiner(new WitParameterValidator());
        private static readonly AssemblyWalker AssemblyWalker = new AssemblyWalker();
        private static readonly ManifestGenerator ManifestGenerator = new ManifestGenerator(AssemblyWalker, AssemblyMiner);
        private static readonly ManifestLoader ManifestLoader = new ManifestLoader();
        private static readonly IWitVRequestFactory VRequestFactory = new WitVRequestFactory();

        private EnumSynchronizer _enumSynchronizer;

        // Tab IDs
        protected const string TAB_APPLICATION_ID = "application";
        protected const string TAB_INTENTS_ID = "intents";
        protected const string TAB_ENTITIES_ID = "entities";
        protected const string TAB_TRAITS_ID = "traits";
        protected const string TAB_VOICES_ID = "voices";
        private string[] _tabIds = new string[] { TAB_APPLICATION_ID, TAB_INTENTS_ID, TAB_ENTITIES_ID, TAB_TRAITS_ID, TAB_VOICES_ID };

        // Generate
        private static ConduitStatistics Statistics
        {
            get
            {
                if (_statistics == null)
                {
                    _statistics = new ConduitStatistics(new PersistenceLayer());
                }
                return _statistics;
            }
        }

        public virtual Texture2D HeaderIcon => WitTexts.HeaderIcon;
        public virtual string HeaderUrl => WitTexts.GetAppURL(configuration.GetApplicationId(), WitTexts.WitAppEndpointType.Settings);
        public virtual string OpenButtonLabel => WitTexts.Texts.WitOpenButtonLabel;

        public void Initialize()
        {
            // Refresh configuration & auth tokens
            configuration = target as WitConfiguration;

            // Get app server token
            _serverToken = WitAuthUtility.GetAppServerToken(configuration);
            if (CanConfigurationRefresh(configuration) && WitConfigurationUtility.IsServerTokenValid(_serverToken))
            {
                // Get client token if needed
                _appID = configuration.GetApplicationId();
                if (string.IsNullOrEmpty(_appID))
                {
                    configuration.SetServerToken(_serverToken);
                }
                // Refresh additional data
                else
                {
                    SafeRefresh();
                }
            }
        }

        public void OnDisable()
        {
            Statistics.Persist();
        }

        public override void OnInspectorGUI()
        {
            // Init if needed
            if (!_initialized || configuration != target)
            {
                Initialize();
                _initialized = true;
            }

            // Draw header
            if (drawHeader)
            {
                WitEditorUI.LayoutHeaderButton(HeaderIcon, HeaderUrl);
                GUILayout.Space(WitStyles.HeaderPaddingBottom);
                EditorGUI.indentLevel++;
            }

            // Layout content
            LayoutContent();

            // Undent
            if (drawHeader)
            {
                EditorGUI.indentLevel--;
            }
        }

        private void GenerateManifestIfNeeded()
        {
            if (!configuration.useConduit || configuration == null)
            {
                return;
            }

            // Get full manifest path & ensure it exists
            string manifestPath = configuration.GetManifestEditorPath();
            manifestAvailable = File.Exists(manifestPath);

            // Auto-generate manifest
            if (!manifestAvailable)
            {
                GenerateManifest(configuration, false);
            }
        }

        private void LayoutConduitContent()
        {
            if (!WitConfigurationUtility.IsServerTokenValid(_serverToken))
            {
                GUILayout.TextArea(WitTexts.Texts.ConfigurationConduitMissingTokenLabel, WitStyles.LabelError);
                return;
            }

            // Set conduit
            var useConduit = (GUILayout.Toggle(configuration.useConduit, "Use Conduit (Beta)"));
            if (configuration.useConduit != useConduit)
            {
                configuration.useConduit = useConduit;
                EditorUtility.SetDirty(configuration);
            }

            GenerateManifestIfNeeded();

            // Configuration buttons
            EditorGUI.indentLevel++;
            GUILayout.Space(EditorGUI.indentLevel * WitStyles.ButtonMargin);
            {
                GUI.enabled = configuration.useConduit;
                GUILayout.BeginHorizontal();
                if (WitEditorUI.LayoutTextButton(manifestAvailable ? "Update Manifest" : "Generate Manifest"))
                {
                    GenerateManifest(configuration, true);
                }
                GUI.enabled = configuration.useConduit && manifestAvailable;
                if (WitEditorUI.LayoutTextButton("Select Manifest") && manifestAvailable)
                {
                    Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(configuration.GetManifestEditorPath());
                }
                GUI.enabled = configuration.useConduit;
                if (WitEditorUI.LayoutTextButton("Specify Assemblies"))
                {
                    PresentAssemblySelectionDialog();
                }
                GUILayout.FlexibleSpace();
                GUI.enabled = configuration.useConduit && manifestAvailable && !syncInProgress;
                if (WitEditorUI.LayoutTextButton("Sync Entities"))
                {
                    SyncEntities();
                }
                if (_isAutoTrainAvailable) {
                    GUI.enabled = configuration.useConduit && manifestAvailable && !syncInProgress;
                    if (WitEditorUI.LayoutTextButton("Auto train") && manifestAvailable)
                    {
                        SyncEntities(() =>
                        {
                            AutoTrainOnWitAi(configuration);
                        });
                    }
                }
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
            EditorGUI.indentLevel--;
        }

        protected virtual void LayoutContent()
        {
            // Begin vertical box
            GUILayout.BeginVertical(EditorStyles.helpBox);

            // Check for app name/id update
            ReloadAppData();

            // Title Foldout
            GUILayout.BeginHorizontal();
            string foldoutText = WitTexts.Texts.ConfigurationHeaderLabel;
            if (!string.IsNullOrEmpty(_appName))
            {
                foldoutText = foldoutText + " - " + _appName;
            }

            _foldout = WitEditorUI.LayoutFoldout(new GUIContent(foldoutText), _foldout);
            // Refresh button
            if (CanConfigurationRefresh(configuration))
            {
                if (string.IsNullOrEmpty(_appName))
                {
                    bool isValid =  WitConfigurationUtility.IsServerTokenValid(_serverToken);
                    GUI.enabled = isValid;
                    if (WitEditorUI.LayoutTextButton(WitTexts.Texts.ConfigurationRefreshButtonLabel))
                    {
                        ApplyServerToken(_serverToken);
                    }
                }
                else
                {
                    bool isRefreshing = configuration.IsRefreshingData();
                    GUI.enabled = !isRefreshing;
                    if (WitEditorUI.LayoutTextButton(isRefreshing ? WitTexts.Texts.ConfigurationRefreshingButtonLabel : WitTexts.Texts.ConfigurationRefreshButtonLabel))
                    {
                        SafeRefresh();
                    }
                }
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Space(WitStyles.ButtonMargin);

            // Show configuration app data
            if (_foldout)
            {
                // Indent
                EditorGUI.indentLevel++;

                // Server access token
                bool updated = false;
                WitEditorUI.LayoutPasswordField(WitTexts.ConfigurationServerTokenContent, ref _serverToken, ref updated);
                if (updated && WitConfigurationUtility.IsServerTokenValid(_serverToken))
                {
                    ApplyServerToken(_serverToken);
                }

                // Additional data
                if (configuration)
                {
                    LayoutConfigurationData();
                }

                // Undent
                EditorGUI.indentLevel--;
            }

            // End vertical box layout
            GUILayout.EndVertical();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            LayoutConduitContent();
            GUILayout.EndVertical();

            // Layout configuration request tabs
            LayoutConfigurationRequestTabs();

            // Additional open wit button
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(OpenButtonLabel, WitStyles.TextButton))
            {
                Application.OpenURL(HeaderUrl);
            }
        }
        // Reload app data if needed
        private void ReloadAppData()
        {
            // Check for changes
            string checkID = "";
            string checkName = "";
            if (configuration != null)
            {
                checkID = configuration.GetApplicationId();
                if (!string.IsNullOrEmpty(checkID))
                {
                    checkName = configuration.GetApplicationInfo().name;
                }
            }
            // Reset
            if (!string.Equals(_appName, checkName) || !string.Equals(_appID, checkID))
            {
                // Refresh app data
                _appName = checkName;
                _appID = checkID;

                // Do not clear token if failed to set
                string newToken = WitAuthUtility.GetAppServerToken(configuration);
                if (!string.IsNullOrEmpty(newToken))
                {
                    _serverToken = newToken;
                }
            }
        }
        // Apply server token
        public void ApplyServerToken(string newToken)
        {
            if (newToken != _serverToken)
            {
                _serverToken = newToken;
                configuration.ResetData();
            }

            WitAuthUtility.ServerToken = _serverToken;
            configuration.SetServerToken(_serverToken);

            GenerateManifestIfNeeded();
        }
        // Whether or not to allow a configuration to refresh
        protected virtual bool CanConfigurationRefresh(WitConfiguration configuration)
        {
            return configuration;
        }
        // Layout configuration data
        protected virtual void LayoutConfigurationData()
        {
            // Reset update
            bool updated = false;
            // Client access field
            string clientAccessToken = configuration.GetClientAccessToken();
            WitEditorUI.LayoutPasswordField(WitTexts.ConfigurationClientTokenContent, ref clientAccessToken, ref updated);
            if (updated && string.IsNullOrEmpty(clientAccessToken))
            {
                VLog.E("Client access token is not defined. Cannot perform requests with '" + configuration.name + "'.");
            }
            // Timeout field
            WitEditorUI.LayoutIntField(WitTexts.ConfigurationRequestTimeoutContent, ref configuration.timeoutMS, ref updated);
            // Updated
            if (updated)
            {
                configuration.SetClientAccessToken(clientAccessToken);
            }

            // Show configuration app data
            LayoutConfigurationEndpoint();
        }
        // Layout endpoint data
        protected virtual void LayoutConfigurationEndpoint()
        {
            // Generate if needed
            if (configuration.endpointConfiguration == null)
            {
                configuration.endpointConfiguration = new WitEndpointConfig();
                EditorUtility.SetDirty(configuration);
            }

            // Handle via serialized object
            var serializedObj = new SerializedObject(configuration);
            var serializedProp = serializedObj.FindProperty("endpointConfiguration");
            EditorGUILayout.PropertyField(serializedProp);
            serializedObj.ApplyModifiedProperties();
        }
        // Tabs
        protected virtual void LayoutConfigurationRequestTabs()
        {
            // Application info
            Meta.WitAi.Data.Info.WitAppInfo appInfo = configuration.GetApplicationInfo();

            // Indent
            EditorGUI.indentLevel++;

            // Iterate tabs
            if (_tabIds != null)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < _tabIds.Length; i++)
                {
                    // Enable if not selected
                    GUI.enabled = _requestTab != i;
                    // If valid and clicked, begin selecting
                    string tabPropertyID = _tabIds[i];
                    if (ShouldTabShow(appInfo, tabPropertyID))
                    {
                        if (WitEditorUI.LayoutTabButton(GetTabText(configuration, appInfo, tabPropertyID, true)))
                        {
                            _requestTab = i;
                        }
                    }
                    // If invalid, stop selecting
                    else if (_requestTab == i)
                    {
                        _requestTab = -1;
                    }
                }

                GUI.enabled = true;
                GUILayout.EndHorizontal();

                // Layout selected tab using property id
                string propertyID = _requestTab >= 0 && _requestTab < _tabIds.Length
                    ? _tabIds[_requestTab]
                    : string.Empty;
                if (!string.IsNullOrEmpty(propertyID) && configuration != null)
                {
                    SerializedObject serializedObj = new SerializedObject(configuration);
                    SerializedProperty serializedProp = serializedObj.FindProperty(GetPropertyName(propertyID));
                    if (serializedProp == null)
                    {
                        WitEditorUI.LayoutErrorLabel(GetTabText(configuration, appInfo, propertyID, false));
                    }
                    else if (!serializedProp.isArray)
                    {
                        EditorGUILayout.PropertyField(serializedProp);
                    }
                    else if (serializedProp.arraySize == 0)
                    {
                        WitEditorUI.LayoutErrorLabel(GetTabText(configuration, appInfo, propertyID, false));
                    }
                    else
                    {
                        for (int i = 0; i < serializedProp.arraySize; i++)
                        {
                            SerializedProperty serializedPropChild = serializedProp.GetArrayElementAtIndex(i);
                            EditorGUILayout.PropertyField(serializedPropChild);
                        }
                    }

                    serializedObj.ApplyModifiedProperties();
                }
            }

            // Undent
            EditorGUI.indentLevel--;
        }
        // Determine if tab should show
        protected virtual bool ShouldTabShow(Meta.WitAi.Data.Info.WitAppInfo appInfo, string tabID)
        {
            if(string.IsNullOrEmpty(appInfo.id))
            {
                return false;
            }

            switch (tabID)
            {
                case TAB_INTENTS_ID:
                    return null != appInfo.intents;
                case TAB_ENTITIES_ID:
                    return null != appInfo.entities;
                case TAB_TRAITS_ID:
                    return null != appInfo.traits;
                case TAB_VOICES_ID:
                    return null != appInfo.voices;
            }

            return true;
        }
        // Determine if tab should show
        protected virtual string GetPropertyName(string tabID)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("_appInfo");
            switch (tabID)
            {
                case TAB_INTENTS_ID:
                    sb.Append($".{TAB_INTENTS_ID}");
                    break;
                case TAB_ENTITIES_ID:
                    sb.Append($".{TAB_ENTITIES_ID}");
                    break;
                case TAB_TRAITS_ID:
                    sb.Append($".{TAB_TRAITS_ID}");
                    break;
                case TAB_VOICES_ID:
                    sb.Append($".{TAB_VOICES_ID}");
                    break;
            }
            return sb.ToString();
        }
        // Get tab text
        protected virtual string GetTabText(WitConfiguration configuration, Meta.WitAi.Data.Info.WitAppInfo appInfo, string tabID, bool titleLabel)
        {
            switch (tabID)
            {
                case TAB_APPLICATION_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationApplicationTabLabel : WitTexts.Texts.ConfigurationApplicationMissingLabel;
                case TAB_INTENTS_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationIntentsTabLabel : WitTexts.Texts.ConfigurationIntentsMissingLabel;
                case TAB_ENTITIES_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationEntitiesTabLabel : WitTexts.Texts.ConfigurationEntitiesMissingLabel;
                case TAB_TRAITS_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationTraitsTabLabel : WitTexts.Texts.ConfigurationTraitsMissingLabel;
                case TAB_VOICES_ID:
                    return titleLabel ? WitTexts.Texts.ConfigurationVoicesTabLabel : WitTexts.Texts.ConfigurationVoicesMissingLabel;
            }
            return string.Empty;
        }

        // Safe refresh
        protected virtual void SafeRefresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (WitConfigurationUtility.IsServerTokenValid(_serverToken))
            {
                configuration.SetServerToken(_serverToken);
            }
            else if (WitConfigurationUtility.IsClientTokenValid(configuration.GetClientAccessToken()))
            {
                configuration.RefreshAppInfo();
            }

            CheckAutoTrainAvailabilityIfNeeded();
        }

        private void CheckAutoTrainAvailabilityIfNeeded()
        {
            if (_didCheckAutoTrainAvailability || !WitConfigurationUtility.IsServerTokenValid(_serverToken)) {
                return;
            }

            _didCheckAutoTrainAvailability = true;
            CheckAutoTrainIsAvailable(configuration, (isAvailable) => {
                _isAutoTrainAvailable = isAvailable;
            });
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded() {
            foreach (var witConfig in WitConfigurationUtility.WitConfigs)
            {
                if (witConfig.useConduit)
                {
                    GenerateManifest(witConfig, false);
                }
            }
        }

        /// <summary>
        /// Generates a manifest and optionally opens it in the editor.
        /// </summary>
        /// <param name="configuration">The configuration that we are generating the manifest for.</param>
        /// <param name="openManifest">If true, will open the manifest file in the code editor.</param>
        private static void GenerateManifest(WitConfiguration configuration, bool openManifest)
        {
            AssemblyWalker.AssembliesToIgnore = new HashSet<string>(configuration.excludedAssemblies);

            // Generate
            var startGenerationTime = DateTime.UtcNow;
            var appInfo = configuration.GetApplicationInfo();
            var manifest = ManifestGenerator.GenerateManifest(appInfo.name, appInfo.id);
            var endGenerationTime = DateTime.UtcNow;

            // Get file path
            var fullPath = configuration.GetManifestEditorPath();
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
            {
                fullPath = GetManifestPullPath(configuration, true);
            }

            // Write to file
            try
            {
                var writer = new StreamWriter(fullPath);
                writer.WriteLine(manifest);
                writer.Close();
            }
            catch (Exception e)
            {
                VLog.E($"Conduit manifest failed to generate\nPath: {fullPath}\n{e}");
                return;
            }

            Statistics.SuccessfulGenerations++;
            Statistics.AddFrequencies(AssemblyMiner.SignatureFrequency);
            Statistics.AddIncompatibleFrequencies(AssemblyMiner.IncompatibleSignatureFrequency);
            var generationTime = endGenerationTime - startGenerationTime;
            var unityPath = fullPath.Replace(Application.dataPath, "Assets");
            AssetDatabase.ImportAsset(unityPath);

            var configName = configuration.name;
            var manifestName = Path.GetFileNameWithoutExtension(unityPath);
            #if UNITY_2021_2_OR_NEWER
            var configPath = AssetDatabase.GetAssetPath(configuration);
            configName = $"<a href=\"{configPath}\">{configName}</a>";
            manifestName = $"<a href=\"{unityPath}\">{manifestName}</a>";
            #endif
            VLog.D($"Conduit manifest generated\nConfiguration: {configName}\nManifest: {manifestName}\nGeneration Time: {generationTime.TotalMilliseconds} ms");

            if (openManifest)
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(fullPath, 1);
            }
        }

        // Show dialog to disable/enable assemblies
        private void PresentAssemblySelectionDialog()
        {
            var assemblyNames = AssemblyWalker.GetAllAssemblies().Select(a => a.FullName).ToList();
            AssemblyWalker.AssembliesToIgnore = new HashSet<string>(configuration.excludedAssemblies);
            WitMultiSelectionPopup.Show(assemblyNames, AssemblyWalker.AssembliesToIgnore, (disabledAssemblies) => {
                AssemblyWalker.AssembliesToIgnore = new HashSet<string>(disabledAssemblies);
                configuration.excludedAssemblies = new List<string>(AssemblyWalker.AssembliesToIgnore);
                GenerateManifestIfNeeded();
            });
        }

        // Sync entities
        private void SyncEntities(Action successCallback = null)
        {
            // Fail without server token
            var validServerToken = WitConfigurationUtility.IsServerTokenValid(_serverToken);
            if (!validServerToken)
            {
                VLog.E($"Conduit Sync Failed\nError: Invalid server token");
                return;
            }

            // Generate
            if (_enumSynchronizer == null)
            {
                _enumSynchronizer = new EnumSynchronizer(configuration, AssemblyWalker, new FileIo(), new WitHttp(configuration.GetServerAccessToken(), 5000), VRequestFactory);
            }

            // Sync
            syncInProgress = true;
            GenerateManifest(configuration, false);
            var manifest = ManifestLoader.LoadManifest(configuration.ManifestLocalPath);
            CoroutineUtility.StartCoroutine(_enumSynchronizer.SyncWitEntities(manifest, (success, data) =>
            {
                syncInProgress = false;
                if (!success)
                {
                    VLog.E($"Conduit failed to synchronize entities\nError: {data}");
                }
                else
                {
                    Debug.Log("Conduit entities successfully synchronized");
                    successCallback?.Invoke();
                }
            }));
        }

        private static void AutoTrainOnWitAi(WitConfiguration configuration)
        {
            var manifest = ManifestLoader.LoadManifest(configuration.ManifestLocalPath);
            var intents = ManifestGenerator.ExtractManifestData();
            VLog.D($"Auto training on WIT.ai: {intents.Count} intents.");

            configuration.ImportData(manifest, (isSuccess) => {
                if (isSuccess) {
                    EditorUtility.DisplayDialog("Auto Train", "Successfully started auto train process on WIT.ai.", "OK");
                } else {
                    EditorUtility.DisplayDialog("Auto Train", "Failed to start auto train process on WIT.ai.", "OK");
                }
            });
        }

        private static void CheckAutoTrainIsAvailable(WitConfiguration configuration, Action<bool> onComplete)
        {
            Meta.WitAi.Data.Info.WitAppInfo appInfo = configuration.GetApplicationInfo();
            string manifestText = ManifestGenerator.GenerateEmptyManifest(appInfo.name, appInfo.id);
            var manifest = ManifestLoader.LoadManifestFromString(manifestText);
            configuration.ImportData(manifest, onComplete);
        }

        private static string GetManifestPullPath(WitConfiguration configuration, bool shouldCreateDirectoryIfNotExist = false)
        {
            string directory = Application.dataPath + "/Oculus/Voice/Resources";
            if (shouldCreateDirectoryIfNotExist)
            {
                IOUtility.CreateDirectory(directory, true);
            }
            return directory + "/" + configuration.ManifestLocalPath;
        }
    }
}
