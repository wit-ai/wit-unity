/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.ComponentModel;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// Lists internal error codes used by the VSDK.
    /// </summary>
    public enum KnownErrorCode
    {
        [Description("File a bug report.")]
        Unknown,

        [Description("Inspect the VLogger class or report this issue to the Voice SDK team.")]
        Logging,

        [Description("Check that all enums that have the ConduitValue attribute applied are valid.")]
        AssemblyMinerNullEnum,

        [Description("Ensure all codes in the KnownErrorCode enum have Description attributes.")]
        KnownErrorMissingDescription,

        [Description("Check that the referenced assembly did not have methods stripped during compilation.")]
        NullMethodInAssembly,

        [Description("Check that the referenced assembly did not have data types stripped during compilation.")]
        NullDeclaringTypeInAssembly,

        [Description("Conduit error handlers (those marked with HandleEntityResolutionFailure) need two parameters. The first should be a string and the second should be an exception.")]
        InvalidErrorHandlerParameter
    }
}
