/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.Windows
{
    public static class WitEditorMenu
    {
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
    }
}
