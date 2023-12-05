/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.WitAi.Events
{
    /// <summary>
    /// An event including raw float sample data from an input mic
    /// <param name="samples">The raw float sample buffer</param>
    /// <param name="sampleCount">The number of samples in the buffer</param>
    /// <param name="maxLevel">The max volume in this sample</param>
    /// </summary>
    [Serializable]
    public class WitSampleEvent : UnityEvent<float[], int, float>
    {
    }
}
