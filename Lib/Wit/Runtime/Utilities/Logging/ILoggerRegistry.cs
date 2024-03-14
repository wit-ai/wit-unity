/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Lib.Wit.Runtime.Utilities.Logging
{
    /// <summary>
    /// Used to created VSDK loggers.
    /// </summary>
    public interface ILoggerRegistry
    {
        IVLogger GetLogger();
        IVLogger GetLogger(string category);
    }
}
