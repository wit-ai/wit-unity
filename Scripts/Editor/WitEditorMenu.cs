/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using Facebook.WitAi.Data;
using Facebook.WitAi.Inspectors;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Configuration;
using Facebook.WitAi.Data.Intents;
using Facebook.WitAi.Data.Entities;
using Facebook.WitAi.Data.Traits;
using Facebook.WitAi.CallbackHandlers;

namespace Facebook.WitAi.Windows
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

        #region DRAWERS
        [CustomPropertyDrawer(typeof(WitEndpointConfig))]
        public class WitCustomEndpointPropertyDrawer : WitEndpointConfigDrawer
        {

        }
        [CustomPropertyDrawer(typeof(WitApplication))]
        public class WitCustomApplicationPropertyDrawer : WitApplicationPropertyDrawer
        {

        }
        [CustomPropertyDrawer(typeof(WitIntent))]
        public class WitCustomIntentPropertyDrawer : WitIntentPropertyDrawer
        {

        }
        [CustomPropertyDrawer(typeof(WitEntity))]
        public class WitCustomEntityPropertyDrawer : WitEntityPropertyDrawer
        {

        }
        [CustomPropertyDrawer(typeof(WitTrait))]
        public class WitCustomTraitPropertyDrawer : WitTraitPropertyDrawer
        {

        }
        [CustomPropertyDrawer(typeof(ValuePathMatcher))]
        public class WitCustomValuePathMatcherPropertyDrawer : ValuePathMatcherPropertyDrawer
        {

        }
        #endregion
    }
}
