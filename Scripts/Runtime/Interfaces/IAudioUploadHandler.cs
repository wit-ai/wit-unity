/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.Data;

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// An interface for uploading chunks of audio data
    /// </summary>
    public interface IAudioUploadHandler : IDataUploadHandler
    {
        /// <summary>
        /// Whether the upload service is ready for uploading data
        /// </summary>
        bool IsInputStreamReady { get; }

        /// <summary>
        /// Callback when service is ready for uploading data
        /// </summary>
        Action OnInputStreamReady { get; set; }

        /// <summary>
        /// The audio encoding to be used for uploading
        /// </summary>
        AudioEncoding AudioEncoding { get; set; }
    }
}
