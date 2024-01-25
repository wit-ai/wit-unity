/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine.Events;

namespace Meta.WitAi
{
    /// <summary>
    /// A static extension class that provides single line implementation for
    /// adding or removing UnityEvent listeners
    /// </summary>
    public static class UnityEventExtensions
    {
        /// <summary>
        /// Either adds or removes a call action with no parameters based on the provided boolean
        /// </summary>
        /// <param name="baseEvent">The base event for the action to be</param>
        /// <param name="call">The action to be called when baseEvent is invoked</param>
        /// <param name="add">If true, the action is added as a listener.  If false,
        /// it is removed.</param>
        public static void SetListener(this UnityEvent baseEvent, UnityAction call, bool add)
        {
            if (baseEvent == null || call == null)
            {
                return;
            }
            if (add)
            {
                baseEvent.AddListener(call);
            }
            else
            {
                baseEvent.RemoveListener(call);
            }
        }

        /// <summary>
        /// Either adds or removes a call action with a single parameter based on the provided boolean
        /// </summary>
        /// <param name="baseEvent">The base event for the action to be</param>
        /// <param name="call">The action to be called when baseEvent is invoked</param>
        /// <param name="add">If true, the action is added as a listener.  If false,
        /// it is removed.</param>
        public static void SetListener<T>(this UnityEvent<T> baseEvent, UnityAction<T> call, bool add)
        {
            if (baseEvent == null || call == null)
            {
                return;
            }
            if (add)
            {
                baseEvent.AddListener(call);
            }
            else
            {
                baseEvent.RemoveListener(call);
            }
        }

        /// <summary>
        /// Either adds or removes a call action with two parameters based on the provided boolean
        /// </summary>
        /// <param name="baseEvent">The base event for the action to be</param>
        /// <param name="call">The action to be called when baseEvent is invoked</param>
        /// <param name="add">If true, the action is added as a listener.  If false,
        /// it is removed.</param>
        public static void SetListener<T0, T1>(this UnityEvent<T0, T1> baseEvent, UnityAction<T0, T1> call, bool add)
        {
            if (baseEvent == null || call == null)
            {
                return;
            }
            if (add)
            {
                baseEvent.AddListener(call);
            }
            else
            {
                baseEvent.RemoveListener(call);
            }
        }

        /// <summary>
        /// Either adds or removes a call action with three parameters based on the provided boolean
        /// </summary>
        /// <param name="baseEvent">The base event for the action to be</param>
        /// <param name="call">The action to be called when baseEvent is invoked</param>
        /// <param name="add">If true, the action is added as a listener.  If false,
        /// it is removed.</param>
        public static void SetListener<T0, T1, T2>(this UnityEvent<T0, T1, T2> baseEvent, UnityAction<T0, T1, T2> call, bool add)
        {
            if (baseEvent == null || call == null)
            {
                return;
            }
            if (add)
            {
                baseEvent.AddListener(call);
            }
            else
            {
                baseEvent.RemoveListener(call);
            }
        }

        /// <summary>
        /// Either adds or removes a call action with four parameters based on the provided boolean
        /// </summary>
        /// <param name="baseEvent">The base event for the action to be</param>
        /// <param name="call">The action to be called when baseEvent is invoked</param>
        /// <param name="add">If true, the action is added as a listener.  If false,
        /// it is removed.</param>
        public static void SetListener<T0, T1, T2, T3>(this UnityEvent<T0, T1, T2, T3> baseEvent, UnityAction<T0, T1, T2, T3> call, bool add)
        {
            if (baseEvent == null || call == null)
            {
                return;
            }
            if (add)
            {
                baseEvent.AddListener(call);
            }
            else
            {
                baseEvent.RemoveListener(call);
            }
        }
    }
}
