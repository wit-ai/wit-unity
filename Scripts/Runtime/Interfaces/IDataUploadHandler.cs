/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Interfaces
{
    /// <summary>
    /// An interface for uploading chunks of data
    /// </summary>
    public interface IDataUploadHandler
    {
        /// <summary>
        /// Writes data to an upload stream.
        /// </summary>
        /// <param name="buffer">The full data to be written</param>
        /// <param name="offset">The starting offset to be written from</param>
        /// <param name="length">The total number of bytes to be written</param>
        void Write(byte[] buffer, int offset, int length);
    }
}
