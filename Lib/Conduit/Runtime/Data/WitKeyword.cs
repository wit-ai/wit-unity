/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi.Data.Info;

namespace Meta.Conduit
{
    public class WitKeyword
    {
        public string keyword;

        public HashSet<string> synonyms;

        public WitKeyword():this("", null)
        {
        }
        
        public WitKeyword(string keyword, List<string> synonyms = null)
        {
            this.keyword = keyword;
            this.synonyms = synonyms == null ? new HashSet<string>() : synonyms.ToHashSet();
        }
        
        public WitKeyword(WitEntityKeywordInfo witEntityKeywordInfo)
        {
            this.keyword = witEntityKeywordInfo.keyword;
            this.synonyms = witEntityKeywordInfo.synonyms.ToHashSet();
        }
        
        public override bool Equals(object obj)
        {
            if (obj is WitKeyword other)
            {
                return Equals(other);
            }

            return false;
        }

        protected bool Equals(WitKeyword other)
        {
            return this.keyword.Equals(other.keyword) && this.synonyms.SequenceEqual(other.synonyms);
        }

        public override int GetHashCode()
        {
            var hash = 17;
            hash = hash * 31 + keyword.GetHashCode();
            hash = hash * 31 + synonyms.GetHashCode();
            return hash;
        }
    }
}
