/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;

namespace Meta.Voice.Logging
{
    /// <summary>
    /// A logging scope encapsulate some operation for more controlled logging.
    /// </summary>
    public interface ILogScope : IDisposable, ICoreLogger
    {

    }
}
