/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.TTS.Data;
using UnityEngine.Events;

namespace Meta.WitAi.TTS.LipSync
{
    /// <summary>
    /// Event that triggers when a viseme is in the process of lerping from one viseme to another
    /// </summary>
    [Serializable]
    public class VisemeLerpEvent : UnityEvent<Viseme, Viseme, float>
    {
        
    }
    
    /// <summary>
    /// Event that triggers when a viseme has fully changed to a new viseme
    /// </summary>
    [Serializable]
    public class VisemeChangedEvent : UnityEvent<Viseme>
    {
        
    }
}
