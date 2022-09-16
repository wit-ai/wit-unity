/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Conduit
{
    public class WitRole
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public override string ToString()
        {
            return $"{this.Name} ({this.Id})";
        }
    }
}
