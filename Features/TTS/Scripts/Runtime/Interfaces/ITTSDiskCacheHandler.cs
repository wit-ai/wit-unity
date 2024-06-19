/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.Interfaces
{
    public interface ITTSDiskCacheHandler
    {
        /// <summary>
        /// The default cache settings
        /// </summary>
        TTSDiskCacheSettings DiskCacheDefaultSettings { get; }

        /// <summary>
        /// A method for obtaining the path to a specific cache clip
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <returns>Returns the clip's cache path</returns>
        string GetDiskCachePath(TTSClipData clipData);

        /// <summary>
        /// Whether or not the clip data should be cached on disk
        /// </summary>
        /// <param name="clipData">Clip request data</param>
        /// <returns>Returns true if should cache</returns>
        bool ShouldCacheToDisk(TTSClipData clipData);
    }
}
