/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Text;
using Meta.WitAi.Data.Info;

namespace Meta.WitAi.Data.Configuration.Tabs
{
    /// <summary>
    /// The Wit Configuration tabs are a set of tabs to be displayed in the inspector
    /// the Wit Configuration. They're made to be dynamic. To add another one, simply
    /// extend this class.
    /// </summary>
    public abstract class WitConfigurationEditorTab
    {
        /// <summary>
        /// The WitConfigurationData type relevant to this tab.
        /// Defaults to Will be null if there is no custom types to reference
        /// </summary>
        public virtual Type DataType => null;

        /// <summary>
        /// The custom ID for this tab
        /// </summary>
        public abstract string TabID { get; }
        /// <summary>
        /// The relative order of the tabs, from 0 upwards.
        /// </summary>
        public abstract int TabOrder { get; }
        /// <summary>
        /// The label to display for this tab.
        /// </summary>
        public abstract string TabLabel { get; }
        /// <summary>
        /// What to show when there is nothing to show
        /// for this tab.
        /// </summary>
        public abstract string MissingLabel { get; }
        /// <summary>
        /// Determines whether or not to show the tab,
        /// based upon the current appInfo.
        /// </summary>
        /// <param name="appInfo">the relevant app info used by this tab, which
        /// can be used to determine the return result</param>
        /// <returns>true if the tab should show, false otherwise</returns>
        public abstract bool ShouldTabShow(WitAppInfo appInfo);

        /// <summary>
        /// Determines whether or not to show the tab,
        /// based upon the current configuration.
        /// </summary>
        /// <param name="configuration">the current configuration which may contain
        /// relevant data to determine the return result</param>
        /// <returns>true if should show, false otherwise</returns>
        public virtual bool ShouldTabShow(WitConfiguration configuration) { return false; }

        /// <param name="tabID"></param>
        /// <returns>the name of the property of the given tabID</returns>
        public virtual string GetPropertyName(string tabID)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("_appInfo");
            sb.Append($".{TabID}");
            return sb.ToString();
        }
        /// <summary>
        /// The text to display for this tab
        /// </summary>
        /// <param name="titleLabel">Whether to display the Tab's label</param>
        public virtual string GetTabText(bool titleLabel)
        {
            return titleLabel ? TabLabel : MissingLabel;
        }
    }
}
