/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using System.Reflection;
using Facebook.WitAi.Data.Traits;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitTrait))]
    public class WitTraitPropertyDrawer : WitPropertyDrawer
    {
        // Get trait category
        protected override string GetLocalizationCategory(SerializedProperty property)
        {
            return WitStyles.ConfigurationResponseTraitsKey;
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
