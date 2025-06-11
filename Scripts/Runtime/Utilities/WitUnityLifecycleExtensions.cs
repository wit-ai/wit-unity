/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

#nullable enable

namespace Meta.WitAi.Utilities
{
    /// <summary>
    /// Provides extension methods for safe object lifecycle management and null checking,
    /// particularly useful for Unity components and NPC system interfaces.
    /// This can be considered a companion to LifecycleExtensions in Meta.Voice.NPCs
    /// </summary>
    public static class WitUnityLifecycleExtensions
    {
        /// <summary>
        /// Checks if an AudioBuffer is null or destroyed
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsDestroyedOrNull([NotNullWhen(false)] this UnityEngine.MonoBehaviour? obj)
        {
            return obj == null;
        }
    }
}
