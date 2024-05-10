/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Data.Configuration;

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// Interface for setting a configuration as well as providing a callback on configuration change
    /// </summary>
    public interface IWitConfigurationSetter
    {
        /// <summary>
        /// The configuration that can be set
        /// </summary>
        WitConfiguration Configuration { get; set; }

        /// <summary>
        /// The callback method on configuration change
        /// </summary>
        event Action<WitConfiguration> OnConfigurationUpdated;
    }
}
