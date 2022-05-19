/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Conduit
{
    /// <summary>
    /// Loads the manifest and resolves its actions so they can be used during dispatching.
    /// </summary>
    internal static class ManifestLoader
    {
        // TODO: Use DI to inject this class rather than using statics.
        /// <summary>
        /// Loads the manifest from file and into a <see cref="Manifest"/> structure.
        /// </summary>
        /// <param name="filePath">The path to the manifest file.</param>
        /// <returns>The loaded manifest object.</returns>
        public static Manifest LoadManifest(string filePath)
        {
            Debug.Log($"Loading Conduit manifest from {filePath}");
            using StreamReader reader = new StreamReader(filePath);
            var rawJson = reader.ReadToEnd();

            var manifest = JsonConvert.DeserializeObject<Manifest>(rawJson);
            manifest.ResolveActions();

            return manifest;
        }
    }
}
