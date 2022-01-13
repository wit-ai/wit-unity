/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Configuration;

namespace Facebook.WitAi.Windows
{
    public class WitWelcomeWizard : ScriptableWizard
    {
        protected Vector2 scrollOffset;
        protected WitConfigurationEditor witEditor;
        protected string serverToken;
        public Action successAction;
        protected virtual GUIContent Title => WitStyles.SetupTitleContent;
        protected virtual Texture2D HeaderIcon => WitStyles.HeaderIcon;
        protected virtual string HeaderUrl => WitStyles.HeaderLinkURL;

        protected virtual void OnEnable()
        {
            titleContent = Title;
        }
        protected virtual void OnWizardCreate()
        {
            ValidateAndClose();
        }
        protected virtual void ValidateAndClose()
        {
            WitAuthUtility.ServerToken = serverToken;
            if (WitAuthUtility.IsServerTokenValid())
            {
                Close();
                if (successAction == null)
                {
                    WitEditorUtility.OpenConfigurationWindow();
                }
                else
                {
                    successAction();
                }
            }
            else
            {
                throw new ArgumentException("Please enter a valid token before linking.");
            }
        }
        protected override bool DrawWizardGUI()
        {
            // Layout window
            Vector2 size = Vector2.zero;
            WitEditorUI.LayoutWindow(titleContent.text, HeaderIcon, HeaderUrl, LayoutContent, ref scrollOffset, out size);

            // Success if token is valid
            return WitAuthUtility.IsServerTokenValid();
        }

        protected virtual float LayoutContent()
        {
            // Center Begin
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();

            // Token
            if (null == serverToken)
            {
                serverToken = WitAuthUtility.ServerToken;
            }

            // TODO: Move to WitStyles
            var color = "blue";
            if (EditorGUIUtility.isProSkin)
            {
                color = "#ccccff";
            }
            string title = $"Paste your <color={color}>Wit.ai</color> Server Access Token here";

            // Layout
            float h = 0f;
            bool updated = false;
            WitEditorUI.LayoutPasswordField(new GUIContent(title), ref serverToken, ref updated, ref h);

            // Updated
            if (updated)
            {
                WitAuthUtility.ServerToken = serverToken;
                ValidateAndClose();
            }

            // Center End
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // Return
            return h;
        }
    }
}
