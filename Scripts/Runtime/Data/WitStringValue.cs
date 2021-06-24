/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;

namespace com.facebook.witai.data
{
    public class WitStringValue : WitValue
    {
        public override object GetValue(WitResponseNode response)
        {
            return GetStringValue(response);
        }

        public override bool Equals(WitResponseNode response, object value)
        {
            if (value is string sValue)
            {
                return GetStringValue(response) == sValue;
            }

            return "" + value == GetStringValue(response);
        }

        public string GetStringValue(WitResponseNode response)
        {
            return Reference.GetStringValue(response);
        }
    }
}
