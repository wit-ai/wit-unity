/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai.data
{
    public abstract class WitValue : ScriptableObject
    {
        [SerializeField] public string path;
        private WitResponseReference reference;

        public WitResponseReference Reference
        {
            get
            {
                if (null == reference)
                {
                    reference = WitResultUtilities.GetWitResponseReference(path);
                }

                return reference;
            }
        }

        public abstract object GetValue(WitResponseNode response);

        public abstract bool Equals(WitResponseNode response, object value);

        public string ToString(WitResponseNode response)
        {
            return Reference.GetStringValue(response);
        }
    }
}
