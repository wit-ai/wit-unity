/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Data.Configuration;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.Windows
{
    public abstract class BaseWitWindow : EditorWindow
    {
        private Vector2 scrollOffset;

        protected abstract GUIContent Title { get; }
        protected virtual Texture2D HeaderIcon => WitStyles.HeaderIcon;
        protected virtual string HeaderUrl => WitStyles.HeaderLinkURL;
        protected virtual void OnEnable()
        {
            titleContent = Title;
            WitConfigurationUtility.RefreshConfigurationList();
        }
        protected virtual void OnDisable()
        {
            scrollOffset = Vector2.zero;
        }
        protected virtual void OnGUI()
        {
            Vector2 size;
            WitEditorUI.LayoutWindow(titleContent.text, HeaderIcon, HeaderUrl, LayoutContent, ref scrollOffset, out size);
            minSize = size;
        }
        protected abstract float LayoutContent();
    }
}
