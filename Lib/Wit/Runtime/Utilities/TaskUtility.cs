/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// A static class used for assistance in various async await scenarios
    /// </summary>
    public static class TaskUtility
    {
        /// <summary>
        /// Awaits an IAsyncResult using Task.Factory.FromAsync
        /// </summary>
        public static Task FromAsyncResult(IAsyncResult asyncResult)
        {
            // Already done
            if (asyncResult.IsCompleted)
            {
                return Task.FromResult(true);
            }
            // Await
            return Task.Factory.FromAsync(asyncResult, StubForTaskFactory);
        }

        // Empty method used since Task.Factory.FromAsync requires a completion method
        private static void StubForTaskFactory(IAsyncResult result) { }

        /// <summary>
        /// Awaits an AsyncOperation by using a
        /// completion source with it's completed delegate
        /// </summary>
        public static Task FromAsyncOp(AsyncOperation asyncOperation)
        {
            // Already done
            if (asyncOperation.isDone)
            {
                return Task.FromResult(true);
            }
            // Generate completion source & set result on complete
            var completion = new TaskCompletionSource<bool>();
            asyncOperation.completed += operation =>
            {
                completion.SetResult(true);
            };
            // Return completion source
            return completion.Task;
        }

        /// <summary>
        /// Awaits a timeout with the option for updated last update and a completion task if desired to quit early
        /// </summary>
        public static async Task WaitForTimeout(int timeoutMs,
            Func<DateTime> getLastUpdate = null,
            Task completionTask = null)
        {
            // Use the timeout provided as the current timeout
            var currentTimeout = timeoutMs;
            while (currentTimeout > 0)
            {
                // If completion task exists, allow early completion
                if (completionTask != null)
                {
                    var task = await Task.WhenAny(completionTask, Task.Delay(currentTimeout));
                    if (task.Equals(completionTask))
                    {
                        return;
                    }
                }
                // Await current timeout only
                else
                {
                    await Task.Delay(currentTimeout);
                }

                // Check if a new timeout is required due to an update since we began
                var now = DateTime.UtcNow;
                var lastUpdate = getLastUpdate != null ? getLastUpdate.Invoke() : now;
                var elapsed = (now - lastUpdate).TotalMilliseconds;
                currentTimeout = Mathf.Max(0, timeoutMs - (int)elapsed);
            }
        }
    }
}
