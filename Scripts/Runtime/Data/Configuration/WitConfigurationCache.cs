/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Meta.WitAi.Data.Configuration
{
    /// <summary>
    /// The various keys required to make a unique wit configuration within a cache
    /// </summary>
    public struct WitConfigurationCacheKey
    {
      /// <summary>
      /// Unique token used to make requests
      /// </summary>
      public string clientAccessToken;

      /// <summary>
      /// Unique version tag to be used for all
      /// </summary>
      public string versionTag;
    }

    /// <summary>
    /// Caching mechanism for WitConfigurations.  Especially useful to consolidate web socket clients.
    /// </summary>
    public class WitConfigurationCache
    {
        /// <summary>
        /// The various configurations to be used
        /// </summary>
        private ConcurrentDictionary<string, WitConfiguration> _configurations = new();

        /// <summary>
        /// The total references for each configuration
        /// </summary>
        private ConcurrentDictionary<string, int> _references = new();

        /// <summary>
        /// Obtains the unique cache id for
        /// </summary>
        public string GetCacheId(WitConfigurationCacheKey key) => $"{key.clientAccessToken}_{key.versionTag}";

        /// <summary>
        /// Obtains a cache key from the configuration
        /// </summary>
        public WitConfigurationCacheKey GetCacheKey(WitConfiguration configuration)
          => new ()
          {
            clientAccessToken = configuration?.GetClientAccessToken(),
            versionTag = configuration?.GetVersionTag()
          };

        /// <summary>
        /// Get cache id from configuration
        /// </summary>
        public string GetCacheId(WitConfiguration configuration) => GetCacheId(GetCacheKey(configuration));

        /// <summary>
        /// Get a configuration based on the specified token and version
        /// </summary>
        public WitConfiguration Get(WitConfigurationCacheKey key, Action<WitConfiguration> onSetup = null)
        {
            // If invalid, ignore
            var id = GetCacheId(key);
            if (string.IsNullOrEmpty(id)) return null;

            // If found, increment and return
            if (_configurations.TryGetValue(id, out var configuration))
            {
                _references[id]++;
                return configuration;
            }

            // Generate configuration
            var newConfiguration = ScriptableObject.CreateInstance<WitConfiguration>();
            newConfiguration.SetClientAccessToken(key.clientAccessToken);
            newConfiguration.editorVersionTag = key.versionTag;
            newConfiguration.buildVersionTag = key.versionTag;
            _configurations[id] = newConfiguration;
            _references[id] = 1;
            onSetup?.Invoke(newConfiguration);
            return newConfiguration;
        }

        /// <summary>
        /// Returns a configuration
        /// </summary>
        public bool Return(WitConfiguration configuration, Action<WitConfiguration> onDestroy = null)
        {
            // If invalid, ignore
            if (configuration == null) return false;
            var id = GetCacheId(configuration);
            if (string.IsNullOrEmpty(id)
                || !_references.TryGetValue(id, out var references))
            {
                return false;
            }

            // Decrement reference count
            references -= 1;
            if (references > 0)
            {
                _references[id] = references;
                return false;
            }

            // Remove from lists and destroy
            _configurations.TryRemove(id, out var discard);
            _references.TryRemove(id, out var discard2);
            onDestroy?.Invoke(configuration);
            MonoBehaviour.Destroy(configuration);
            return true;
        }
    }
}
