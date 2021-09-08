/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.events;
using UnityEngine.Events;

namespace com.facebook.witai.interfaces
{
    public interface IEntityListProvider
    {
        /// <summary>
        /// Used to get Dynamic Entities
        /// </summary>
        string ToJSON();
    }
}
