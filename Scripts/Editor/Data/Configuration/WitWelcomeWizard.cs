/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEditor;
using UnityEngine;

namespace Facebook.WitAi.Data.Configuration
{
    public class WitWelcomeWizard : ScriptableWizard
    {
        protected WitConfigurationEditor witEditor;
        protected string serverToken;
        public Action successAction;

        protected virtual string TitleText => "Welcome to Wit.AI";
        protected virtual Texture2D TitleIcon => WitStyles.WitIcon;
        protected virtual void OnEnable()
        {
            titleContent = new GUIContent(TitleText, TitleIcon);
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
            maxSize = minSize = new Vector2(400, 375);
            BaseWitWindow.DrawHeader("https://wit.ai/apps");

            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.BeginVertical();
            GUILayout.Label("Build Natural Language Experiences", WitStyles.LabelHeader);
            GUILayout.Label(
                "Empower people to use your product with voice and text",
                WitStyles.LabelHeader2);
            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.EndHorizontal();
            GUILayout.Space(32);

            BaseWitWindow.BeginCenter(296);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Paste your Server Access Token here", WitStyles.Label);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(WitStyles.PasteIcon, WitStyles.Label))
            {
                serverToken = EditorGUIUtility.systemCopyBuffer;
                WitAuthUtility.ServerToken = serverToken;
                ValidateAndClose();
            }
            GUILayout.EndHorizontal();
            if (null == serverToken)
            {
                serverToken = WitAuthUtility.ServerToken;
            }
            serverToken = EditorGUILayout.PasswordField(serverToken, WitStyles.TextField);
            BaseWitWindow.EndCenter();

            return WitAuthUtility.IsServerTokenValid();
        }
    }
}
