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

        protected virtual void OnEnable()
        {
            WitAuthUtility.InitEditorTokens();
            titleContent = GetTitle();
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
            WitEditorUI.LayoutWindow(titleContent.text, GetHeaderIcon(), GetHeaderURL(), LayoutContent, ref scrollOffset, out size);

            // Success if token is valid
            return WitConfigurationUtility.IsServerTokenValid(serverToken);
        }

        /// <summary>
        /// Title
        /// </summary>
        protected virtual GUIContent GetTitle()
        {
            return WitStyles.SetupTitleContent;
        }
        /// <summary>
        /// Header icon getter
        /// </summary>
        protected virtual Texture2D GetHeaderIcon()
        {
            return WitStyles.HeaderIcon;
        }
        /// <summary>
        /// Header url getter
        /// </summary>
        protected virtual string GetHeaderURL()
        {
            return WitStyles.Texts.WitUrl;
        }
        /// <summary>
        /// Get header text
        /// </summary>
        protected virtual string GetServerTokenText()
        {
            return WitStyles.Texts.SetupServerTokenLabel;
        }
        /// <summary>
        /// Draw content of window
        /// </summary>
        protected virtual float LayoutContent()
        {
            // Center Begin
            float h = 0f;

            // Token
            if (null == serverToken)
            {
                serverToken = WitAuthUtility.ServerToken;
            }

            // Layout field
            bool updated = false;
            WitEditorUI.LayoutPasswordField(new GUIContent(GetServerTokenText()), ref serverToken, ref updated, ref h);

            // Return
            return h;
        }
    }
}
