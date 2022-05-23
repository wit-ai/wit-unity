/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;
using Meta.Wit.LitJson;
using UnityEngine;

namespace Meta.Conduit
{
    /// <summary>
    /// Loads the manifest and resolves its actions so they can be used during dispatching.
    /// </summary>
    internal class ManifestLoader : IManifestLoader
    {
        /// <summary>
        /// Loads the manifest from file and into a <see cref="Manifest"/> structure.
        /// </summary>
        /// <param name="filePath">The path to the manifest file.</param>
        /// <returns>The loaded manifest object.</returns>
        public Manifest LoadManifest(string filePath)
        {
            Debug.Log($"Loading Conduit manifest from {filePath}");
            string rawJson;
            using (var reader = new StreamReader(filePath))
            {
                rawJson = reader.ReadToEnd();
            }

            var manifest = JsonMapper.ToObject<Manifest>(rawJson);
            manifest.ResolveActions();

            return manifest;
        }
    }
}
