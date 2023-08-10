/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A simple interface for receiving an AudioClip
    /// </summary>
    public interface IAudioClipSetter
    {
        /// <summary>
        /// A method for setting an audio clip
        /// </summary>
        /// <param name="clip">The audio clip to be set</param>
        /// <returns>False if the new clip cannot be set</returns>
        bool SetClip(AudioClip clip);
    }
}
