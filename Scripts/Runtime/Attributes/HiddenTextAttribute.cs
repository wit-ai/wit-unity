/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// Hides text with a Password field unless the user clicks the visibility icon to toggle between password and text mode.
    ///
    /// NOTE: Like any password inspector field, this is serialized in plain text. This is purely about visual obfuscation
    /// </summary>
    public class HiddenTextAttribute : PropertyAttribute
    {

    }
}
