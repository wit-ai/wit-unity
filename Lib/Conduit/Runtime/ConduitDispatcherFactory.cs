/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Conduit
{
    /// <summary>
    /// Creates and caches conduit dispatchers.
    /// </summary>
    public class ConduitDispatcherFactory
    {
        /// <summary>
        /// Dispatcher instance
        /// </summary>
        private static IConduitDispatcher instance;
        
        /// <summary>
        /// Returns a Conduit dispatcher instance. The same instance will be reused past the first request.  
        /// </summary>
        /// <returns>A Conduit dispatcher instance</returns>
        public IConduitDispatcher GetDispatcher()
        {
            return instance ??= new ConduitDispatcher(new ManifestLoader());
        }
    }
}
