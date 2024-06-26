/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Meta.WitAi
{
    /// <summary>
    /// A static class used for adding new Task options
    /// </summary>
    public static class TaskExtensions
    {
        /// <summary>
        /// A task that completes when less than the specified max tasks are running
        /// </summary>
        public static Task WhenLessThan(this ICollection<Task> tasks, int max)
            => WhenLessThan(tasks, max, CancellationToken.None);

        /// <summary>
        /// A task that completes when less than the specified max tasks are running
        /// </summary>
        public static Task WhenLessThan(this ICollection<Task> tasks, int max,
            CancellationToken cancellationToken)
        {
            // Throw without tasks
            if (tasks == null)
            {
                throw new ArgumentNullException(nameof(tasks));
            }

            // Completion task
            var completion = new TaskCompletionSource<bool>();

            // Handle cancellation
            cancellationToken.Register(() => completion.TrySetCanceled());

            // Iterate until hit
            int running = tasks.Count;
            max = Mathf.Max(0, max);
            foreach (var task in tasks)
            {
                if (task.IsCompleted)
                {
                    running--;
                }
                else
                {
                    task.ContinueWith(t =>
                        {
                            if (!completion.Task.IsCompleted
                                && Interlocked.Decrement(ref running) < max)
                            {
                                completion.SetResult(true);
                            }
                        },
                        cancellationToken,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                }
            }

            // Already complete
            if (!completion.Task.IsCompleted
                && running < max)
            {
                completion.SetResult(true);
            }

            // Return task
            return completion.Task;
        }
    }
}
