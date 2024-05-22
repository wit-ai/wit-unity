/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Logging
{
    internal class VLoggerFactory : IVLoggerFactory
    {
        public IVLogger GetLogger(string category, ILogSink logSink)
        {
            return new VLogger(category, logSink);
        }
    }
}
