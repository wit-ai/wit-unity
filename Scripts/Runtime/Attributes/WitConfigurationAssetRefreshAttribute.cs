/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.WitAi.Attributes
{
    /// <summary>
    /// An attribute to tag a method that accepts a Invoke(WitConfiguration, WitConfigurationAssetData)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class WitConfigurationAssetRefreshAttribute : Attribute
    {
    }
}
