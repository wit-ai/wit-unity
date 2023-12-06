/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.WitAi.Json
{
    /// <summary>
    /// An attribute to be used to tag a field/property that should not be serialized/deserialized via JsonConvert
    /// </summary>
    [AttributeUsage(validOn:AttributeTargets.Field|AttributeTargets.Property, AllowMultiple = false)]
    public class JsonIgnoreAttribute : Attribute
    {
    }
}
