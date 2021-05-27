/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.lib;

namespace com.facebook.witai
{
    public class WitResultUtilities
    {
        public static string GetFirstEntityValue(WitResponseNode witResponse, string name)
        {
            return witResponse?["entities"]?[name]?[0]?["value"]?.Value;
        }

        public static WitResponseNode GetFirstEntity(WitResponseNode witResponse, string name)
        {
            return witResponse?["entities"]?[name][0];
        }

        public static string GetIntentName(WitResponseNode witResponse)
        {
            return witResponse?["intents"]?[0]?["name"]?.Value;
        }

        public static WitResponseNode GetFirstIntent(WitResponseNode witResponse)
        {
            return witResponse?["intents"]?[0];
        }
    }
}
