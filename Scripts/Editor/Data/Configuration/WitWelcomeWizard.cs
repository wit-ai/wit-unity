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
    public class WitWelcomeWizard : WitScriptableWizard
    {
        protected string serverToken;
        public Action<WitConfiguration> successAction;

        protected override Texture2D HeaderIcon => WitStyles.HeaderIcon;
        protected override GUIContent Title => WitStyles.SetupTitleContent;
        protected override string ButtonLabel => WitStyles.Texts.SetupSubmitButtonLabel;
        protected override string ContentSubheaderLabel => WitStyles.Texts.SetupSubheaderLabel;

        protected override void OnEnable()
        {
            base.OnEnable();
            serverToken = string.Empty;
            WitAuthUtility.ServerToken = serverToken;
        }
        protected override bool DrawWizardGUI()
        {
            // Layout base
            base.DrawWizardGUI();
            // True if valid server token
            return WitConfigurationUtility.IsServerTokenValid(serverToken);
        }
        protected override void LayoutFields()
        {
            string serverTokenLabelText = WitStyles.Texts.SetupServerTokenLabel;
            serverTokenLabelText = serverTokenLabelText.Replace(WitStyles.WitLinkKey, WitStyles.WitLinkColor);
            if (GUILayout.Button(serverTokenLabelText, WitStyles.Label))
            {
                Application.OpenURL(WitStyles.GetAppURL("", WitStyles.WitAppEndpointType.Settings));
            }
            bool updated = false;
            WitEditorUI.LayoutPasswordField(null, ref serverToken, ref updated);
        }
        protected override void OnWizardCreate()
        {
            ValidateAndClose();
        }
        protected virtual void ValidateAndClose()
        {
            WitAuthUtility.ServerToken = serverToken;
            if (WitAuthUtility.IsServerTokenValid())
            {
                // Create configuration
                int index = CreateConfiguration(serverToken);
                if (index != -1)
                {
                    // Complete
                    Close();
                    WitConfiguration c = WitConfigurationUtility.WitConfigs[index];
                    if (successAction == null)
                    {
                        WitWindowUtility.OpenConfigurationWindow(c);
                    }
                    else
                    {
                        successAction(c);
                    }
                }
            }
            else
            {
                throw new ArgumentException(WitStyles.Texts.SetupSubmitFailLabel);
            }
        }
        protected virtual int CreateConfiguration(string newToken)
        {
            return WitConfigurationUtility.CreateConfiguration(newToken);
        }
    }
}
