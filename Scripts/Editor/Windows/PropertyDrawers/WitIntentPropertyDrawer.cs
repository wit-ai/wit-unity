/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Intents;
using System.Reflection;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitIntent))]
    public class WitIntentPropertyDrawer : WitPropertyDrawer
    {
        // Get intent category
        protected override string GetLocalizationCategory(SerializedProperty property)
        {
            return WitStyles.ConfigurationResponseIntentsKey;
        }
        // Use name value for title if possible
        protected override string GetLocalizedTitle(SerializedProperty property)
        {
            string v = GetFieldStringValue(property, "name");
            if (!string.IsNullOrEmpty(v))
            {
                return v;
            }
            return "???";// base.GetLocalizedTitle(property);
        }
        // Determine if should layout field
        protected override bool ShouldLayoutField(FieldInfo subfield)
        {
            switch (subfield.Name)
            {
                case "name":
                    return false;
            }
            return base.ShouldLayoutField(subfield);
        }
        // Dont let edit
        protected override WitPropertyEditType GetEditType()
        {
            return WitPropertyEditType.NoEdit;
        }
    }
}
