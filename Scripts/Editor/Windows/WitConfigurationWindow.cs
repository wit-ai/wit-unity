/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Data.Configuration;

namespace Facebook.WitAi.Windows
{
    public abstract class WitConfigurationWindow : BaseWitWindow
    {
        protected int witConfigIndex = -1;
        protected WitConfiguration witConfiguration;

        protected override string HeaderUrl
        {
            get
            {
                string appID = WitConfigurationUtility.GetAppID(witConfiguration);
                if (!string.IsNullOrEmpty(appID))
                {
                    return WitStyles.GetSettingsURL(appID);
                }
                return base.HeaderUrl;
            }
        }
        protected virtual void SetConfiguration(int newConfiguration)
        {
            witConfigIndex = newConfiguration;
            WitConfiguration[] witConfigs = WitConfigurationUtility.WitConfigs;
            witConfiguration = witConfigs != null && witConfigIndex >= 0 && witConfigIndex < witConfigs.Length ? witConfigs[witConfigIndex] : null;
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            WitAuthUtility.InitEditorTokens();
        }
        protected override void LayoutContent()
        {
            // Layout popup
            int index = witConfigIndex;
            WitConfigurationEditorUI.LayoutConfigurationSelect(ref index);
            // Selection changed
            if (index != witConfigIndex)
            {
                SetConfiguration(index);
            }
        }
    }
}
