/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Data;
using Meta.WitAi.Inspectors;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.CallbackHandlers;

namespace Meta.WitAi.Windows
{
    public static class WitEditorMenu
    {
        #region WINDOWS
        [MenuItem("Window/Wit/Wit Settings")]
        public static void OpenConfigurationWindow()
        {
            WitWindowUtility.OpenConfigurationWindow();
        }
        [MenuItem("Window/Wit/Understanding Viewer")]
        public static void OpenUnderstandingWindow()
        {
            WitWindowUtility.OpenUnderstandingWindow();
        }
        #endregion

        #region CREATION
        [MenuItem("Assets/Create/Wit/Add Wit to Scene")]
        public static void AddWitToScene()
        {
            WitDataCreation.AddWitToScene();
        }
        [MenuItem("Assets/Create/Wit/Values/String Value")]
        public static void WitCreateStringValue()
        {
            WitDataCreation.CreateStringValue("");
        }
        [MenuItem("Assets/Create/Wit/Values/Float Value")]
        public static void WitCreateFloatValue()
        {
            WitDataCreation.CreateFloatValue("");
        }
        [MenuItem("Assets/Create/Wit/Values/Int Value")]
        public static void WitCreateIntValue()
        {
            WitDataCreation.CreateIntValue("");
        }
        [MenuItem("Assets/Create/Wit/Configuration")]
        public static void WitCreateConfiguration()
        {
            WitConfigurationUtility.CreateConfiguration(WitAuthUtility.ServerToken);
        }
        #endregion

        #region INSPECTORS
        [CustomEditor(typeof(Wit))]
        public class WitCustomInspector : WitInspector
        {

        }
        [CustomEditor(typeof(WitConfiguration))]
        public class WitConfigurationCustomInspector : WitConfigurationEditor
        {

        }
        #endregion
    }
}
