/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using System.Reflection;
using Facebook.WitAi.Data.Entities;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitEntity))]
    public class WitEntityPropertyDrawer : WitPropertyDrawer
    {
        // Get localized category
        protected override string GetLocalizationCategory(SerializedProperty property)
        {
            return WitStyles.ConfigurationResponseEntitiesKey;
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
                case "keywords":
                    return false;
            }
            return base.ShouldLayoutField(subfield);
        }
    }
}
