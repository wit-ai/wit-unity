/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi.TTS.Data;

namespace Meta.WitAi.TTS.LipSync
{
    public interface ILipsyncAnimator
    {
        /// <summary>
        /// Called when a viseme is in the process of lerping from one value to another
        /// </summary>
        /// <param name="oldVieseme">The last viseme shown</param>
        /// <param name="newViseme">The viseme that is being transitioned to</param>
        /// <param name="percentage">The percentage of the progress of transitioning</param>
        void OnVisemeLerp(Viseme oldVieseme, Viseme newViseme, float percentage);
        
        /// <summary>
        /// Called when a viseme has fully changed to a new viseme
        /// </summary>
        /// <param name="viseme"></param>
        void OnVisemeChanged(Viseme viseme);
    }
}
