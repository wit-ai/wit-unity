/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.Conduit
{
    public class WitKeyword
    {
        public string Keyword { get; set; }

        public List<string> Synonyms { get; set; }
    }
}
