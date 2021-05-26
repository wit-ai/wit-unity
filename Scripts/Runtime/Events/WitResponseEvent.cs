/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using com.facebook.witai.lib;
using UnityEngine.Events;

namespace com.facebook.witai.events
{
    [Serializable]
    public class WitResponseEvent : UnityEvent<WitResponseNode>
    {
    }
}
