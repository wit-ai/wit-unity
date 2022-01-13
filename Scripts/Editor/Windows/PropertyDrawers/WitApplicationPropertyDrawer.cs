/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;
using Facebook.WitAi.Data.Configuration;
using System.Reflection;

namespace Facebook.WitAi.Windows
{
    [CustomPropertyDrawer(typeof(WitApplication))]
    public class WitApplicationPropertyDrawer : WitPropertyDrawer
    {
        // No foldout needed
        protected override bool UseFoldout()
        {
            return false;
        }
        // Dont let edit
        protected override WitPropertyEditType GetEditType()
        {
            return WitPropertyEditType.NoEdit;
        }
        // Skip wit configuration field
        protected override bool ShouldLayoutField(FieldInfo subfield)
        {
            switch (subfield.Name)
            {
                case "witConfiguration":
                    return false;
            }
            return base.ShouldLayoutField(subfield);
        }
    }
}
