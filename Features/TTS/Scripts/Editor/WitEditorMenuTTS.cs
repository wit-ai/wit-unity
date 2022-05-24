/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.TTS.Editor.Preload;
using UnityEditor;

namespace Facebook.WitAi.TTS.Editor.Windows
{
    public static class WitEditorMenu
    {
        #region CREATION
        [MenuItem("Assets/Create/Wit/TTS Preload Settings")]
        public static void CreateTTSPreloadSettings()
        {
            TTSPreloadUtility.CreatePreloadSettings();
        }
        #endregion
    }
}
