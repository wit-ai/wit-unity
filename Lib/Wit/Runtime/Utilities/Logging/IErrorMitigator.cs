/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Logging
{
    public interface IErrorMitigator
    {
        /// <summary>
        /// Returns the mitigation for an error code.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <returns>The mitigation.</returns>
        string GetMitigation(ErrorCode errorCode);

        /// <summary>
        /// Adds or replaces a mitigation for an error code.
        /// This is typically used by external packages to provide their own mitigations.
        /// </summary>
        /// <param name="errorCode">The error code.</param>
        /// <param name="mitigation">The mitigation.</param>
        void SetMitigation(ErrorCode errorCode, string mitigation);
    }
}
