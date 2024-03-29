﻿/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Threading.Tasks;

namespace Meta.Conduit
{
    internal interface IManifestLoader
    {
        /// <summary>
        /// Loads the manifest from file and into a <see cref="Manifest"/> structure.
        /// </summary>
        /// <param name="filePath">The path to the manifest file.</param>
        /// <returns>The loaded manifest object.</returns>
        Manifest LoadManifest(string filePath);

        /// <summary>
        /// Loads the manifest from an input string into a <see cref="Manifest"/> structure.
        /// </summary>
        /// <param name="manifestText">Plain text content of Manifest.</param>
        /// <returns>The loaded manifest object.</returns>
        Manifest LoadManifestFromJson(string manifestText);

        /// <summary>
        /// Loads the manifest from file and into a <see cref="Manifest"/> structure asynchronously.
        /// </summary>
        /// <param name="filePath">The path to the manifest file.</param>
        /// <returns>The loaded manifest object.</returns>
        Task<Manifest> LoadManifestAsync(string filePath);

        /// <summary>
        /// Loads the manifest from an input string into a <see cref="Manifest"/> structure asynchronously.
        /// </summary>
        /// <param name="manifestText">Plain text content of Manifest.</param>
        /// <returns>The loaded manifest object.</returns>
        Task<Manifest> LoadManifestFromJsonAsync(string manifestText);
    }
}
