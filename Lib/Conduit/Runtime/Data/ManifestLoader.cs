/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Threading.Tasks;
using Meta.Voice.Logging;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using UnityEngine;

namespace Meta.Conduit
{
    /// <summary>
    /// Loads the manifest and resolves its actions so they can be used during dispatching.
    /// </summary>
    [LogCategory(LogCategory.Conduit)]
    class ManifestLoader : IManifestLoader
    {
        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        /// <inheritdoc/>
        public Manifest LoadManifest(string manifestLocalPath)
        {
            var manifestPath = Path.GetFileNameWithoutExtension(manifestLocalPath);
            var jsonFile = Resources.Load<TextAsset>(manifestPath);
            if (jsonFile == null)
            {
                VLog.E(GetType().Name, $"No Manifest found at Resources/{manifestLocalPath}");
                return null;
            }
            return LoadManifestFromJson(jsonFile.text);
        }

        /// <inheritdoc/>
        public Manifest LoadManifestFromJson(string manifestText)
        {
            var manifest = JsonConvert.DeserializeObject<Manifest>(manifestText);
            if (manifest.ResolveActions())
            {
                _log.Info($"Successfully Loaded Conduit manifest");
            }
            else
            {
                VLog.E(GetType().Name, $"Failed to resolve actions from Conduit manifest");
            }
            return manifest;
        }

        /// <inheritdoc/>
        public async Task<Manifest> LoadManifestAsync(string manifestLocalPath)
        {
            // Get file path
            var manifestPath = Path.GetFileNameWithoutExtension(manifestLocalPath);

            // Load async from resources
            var jsonRequest = Resources.LoadAsync<TextAsset>(manifestPath);
            await TaskUtility.WaitWhile(() => !jsonRequest.isDone);

            // Success
            if (jsonRequest.asset is TextAsset textAsset)
            {
                return await LoadManifestFromJsonAsync(textAsset.text);
            }

            // Failed
            VLog.E(GetType().Name, $"No Manifest found at Resources/{manifestLocalPath}");
            return null;
        }

        /// <inheritdoc/>
        public async Task<Manifest> LoadManifestFromJsonAsync(string manifestText)
        {
            // Wait for manifest to deserialize
            var manifest = await JsonConvert.DeserializeObjectAsync<Manifest>(manifestText);
            if (manifest == null)
            {
                VLog.E(GetType().Name, $"Cannot decode Conduit manifest\n\n{manifestText}");
                return null;
            }

            // Resolve actions on background thread
            await Task.Run(() =>
            {
                try
                {
                    if (manifest.ResolveActions())
                    {
                        _log.Info($"Successfully Loaded Conduit manifest");
                    }
                    else
                    {
                        VLog.E(GetType().Name, $"Failed to decode actions from Conduit manifest");
                    }
                }
                catch (Exception e)
                {
                    VLog.E(GetType().Name, $"Failed to decode actions from Conduit manifest\n{e}");
                }
            });

            // Return
            return manifest;
        }
    }
}
