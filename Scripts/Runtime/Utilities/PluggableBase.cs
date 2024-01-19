/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using Meta.WitAi.Utilities;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// This is a plugin injection system for the given type T which can search for
    /// the given type and store it statically for the entire class.
    ///
    /// It will add a default instance of each type found to the list of Plugins
    /// for use by child classes.
    /// </summary>
    /// <typeparam name="T">the type of plugin</typeparam>
    [Serializable]
    public abstract class PluggableBase<T>
    {
        private static Type[] _pluginTypes;

        /// <summary>
        /// A collection of the instantiated plugins which were found.
        /// </summary>
        [SerializeField]
        protected List<T> LoadedPlugins;

        /// <summary>
        /// Checks whether we've already cached the loaded plugins and
        /// reloads them if we've had code changes
        /// (eg if the static editor code has been updated).
        /// </summary>
        protected static void CheckForPlugins()
        {
            if (_pluginTypes != null)
                return;

            FindPlugins();
        }

        /// <summary>
        /// Looks for plugins and loads them.
        /// </summary>
        protected void EnsurePluginsAreLoaded()
        {
            CheckForPlugins();
            LoadedPlugins = new List<T>(BuildPlugins());
        }

        /// <summary>
        /// Explicitly reloads all the plugin types from current assemblies
        /// in case the assemblies and code have changed.
        /// </summary>
        private static void FindPlugins()
        {
            _pluginTypes = ReflectionUtils.GetAllAssignableTypes<T>();
        }

        /// <summary>
        /// Calls the default constructor on each of the given types.
        /// </summary>
        /// <returns>a collection of plugins</returns>
        private static IEnumerable<T> BuildPlugins()
        {
            T[] results = new T[_pluginTypes.Length];

            for(int i=0;i<results.Length; i++)
            {
                if (Activator.CreateInstance(_pluginTypes[i]) is T plugin)
                {
                    results[i] = plugin;
                }
            }
            //TODO: T175587572 these could be instead cached by type to avoid the Find call
            return results;
        }

        /// <returns>Retrieves the given type from the list of loaded plugins, if it exists.
        /// Returns the default option otherwise</returns>
        /// <typeparam name="TPluginType">The type of plugin to retrieve</typeparam>
        public TPluginType Get<TPluginType>() where TPluginType : T
        {
            if (LoadedPlugins == null)
            {
                EnsurePluginsAreLoaded();
            }
            return (TPluginType)LoadedPlugins.Find(path => path is TPluginType);
        }

        /// <returns>Retrieves all plugins of the given type from the list of loaded plugins,
        /// if that type exists.
        /// Returns the default option otherwise</returns>
        /// <typeparam name="TPluginType">The type of plugin to retrieve</typeparam>
        public TPluginType[] GetAll<TPluginType>() where TPluginType : T
        {
            if (LoadedPlugins == null)
            {
                EnsurePluginsAreLoaded();
            }

            if (LoadedPlugins == null) return Array.Empty<TPluginType>();

            List<TPluginType> plugins = new List<TPluginType>();
            foreach (var plugin in LoadedPlugins)
            {
                if (plugin is TPluginType type)
                {
                    plugins.Add(type);
                }
            }

            return plugins.ToArray();
        }
    }
}
