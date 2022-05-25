/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Reflection;
using Facebook.WitAi.Lib;
using Meta.Conduit;

namespace Facebook.WitAi
{
    internal class WitConduitParameterProvider : ParameterProvider
    {
        public const string WitResponseNodeReservedName = "@WitResponseNode";
        protected override object GetSpecializedParameter(ParameterInfo formalParameter)
        {
            if (formalParameter.ParameterType != typeof(WitResponseNode))
            {
                throw new ArgumentException(nameof(formalParameter));
            }
            
            var parameterValue = this.ActualParameters[WitResponseNodeReservedName];
            if (parameterValue == null)
            {
                throw new NotSupportedException("Missing node parameter");
            }

            return parameterValue;
        }

        protected override bool SupportedSpecializedParameter(ParameterInfo formalParameter)
        {
            return formalParameter.ParameterType == typeof(WitResponseNode);
        }
    }
}
