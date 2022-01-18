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
        protected string serverToken;
        public Action successAction;
        protected virtual GUIContent Title => WitStyles.SetupTitleContent;
        protected virtual Texture2D HeaderIcon => WitStyles.HeaderIcon;
        protected virtual string HeaderUrl => WitStyles.Texts.WitUrl;
        protected virtual string ServerTokenLabel => WitStyles.Texts.SetupServerTokenLabel;

        protected virtual void OnEnable()
        {
            WitAuthUtility.InitEditorTokens();
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
                    WitWindowUtility.OpenConfigurationWindow();
                }
                else
                {
                    successAction();
                }
            }
            else
            {
                throw new ArgumentException(WitStyles.Texts.SetupSubmitFailLabel);
            }
        }
        protected override bool DrawWizardGUI()
        {
            // Layout window
            Vector2 size = Vector2.zero;
            WitEditorUI.LayoutWindow(titleContent.text, HeaderIcon, HeaderUrl, LayoutContent, ref scrollOffset, out size);

            // Success if token is valid
            return WitConfigurationUtility.IsServerTokenValid(serverToken);
        }
        protected virtual void LayoutContent()
        {
            // Get new Token
            if (string.IsNullOrEmpty(serverToken))
            {
                serverToken = WitAuthUtility.ServerToken;
            }

            // Layout field
            bool updated = false;
            WitEditorUI.LayoutPasswordField(new GUIContent(ServerTokenLabel), ref serverToken, ref updated);
        }
    }
}
