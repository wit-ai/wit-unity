/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using com.facebook.witai.interfaces;

namespace com.facebook.witai
{
    public class WitJsonEntityList : IEntityListProvider
    {
        public string  myJson = "{}";
        public WitJsonEntityList(string json)
        {
            myJson = json;
        }
        public string ToJSON()
          {
            return myJson;
          }
    }
}
