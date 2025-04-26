/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.Voice.Logging;

namespace Lib.Wit.Runtime.Utilities.Logging
{
    /// <summary>
    /// This should be implemented by classes that will be writing logs to VLogger.
    /// </summary>
    public interface ILogSource
    {
        IVLogger Logger { get; }
    }
}
