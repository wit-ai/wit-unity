/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Reflection;
using Facebook.WitAi.Data;
using Facebook.WitAi.Lib;
using Meta.Conduit;

namespace Facebook.WitAi
{
    internal class WitConduitParameterProvider : ParameterProvider
    {
        public const string WitResponseNodeReservedName = "@WitResponseNode";
        public const string VoiceSessionReservedName = "@VoiceSession";
        protected override object GetSpecializedParameter(ParameterInfo formalParameter)
        {
            if (!SupportedSpecializedParameter(formalParameter))
            {
                throw new ArgumentException(nameof(formalParameter));
            }

            var parameterValue = this.ActualParameters[WitResponseNodeReservedName];
            if (parameterValue == null)
            {
                throw new NotSupportedException("Missing WitResponseNode parameter");
            }

            parameterValue = this.ActualParameters[VoiceSessionReservedName];
            if (parameterValue == null)
            {
                throw new NotSupportedException("Missing VoiceSession parameter");
            }

            return parameterValue;
        }

        protected override bool SupportedSpecializedParameter(ParameterInfo formalParameter)
        {
            return formalParameter.ParameterType == typeof(WitResponseNode) || formalParameter.ParameterType == typeof(VoiceSession);
        }
    }
}
