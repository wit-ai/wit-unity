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
        /// Method for wrapping and logging any thrown task exceptions
        /// </summary>
        public static void WrapErrors(this Task task)
        {
            task.ContinueWith((t, state) =>
            {
                if (t.Exception != null
                    && !(t.Exception.InnerException != null
                         && t.Exception.InnerException.Message.Equals(WitConstants.CANCEL_ERROR)))
                {
                    VLog.E(t.Exception);
                }
            }, null);
        }

        /// <summary>
        /// Method for throwing any task exceptions even hidden cancellations
        /// </summary>
        /// <param name="preThrow">Optional action that calls prior to throwing an exception</param>
        public static void ThrowCaughtExceptions(this Task task, Action<string> preThrow = null)
        {
            // Throw the exception itself
            if (task.Exception?.InnerException != null)
            {
                preThrow?.Invoke(task.Exception.InnerException.Message);
                throw task.Exception.InnerException;
            }
            // Throw cancellation exceptions
            if (task.IsCanceled)
            {
                preThrow?.Invoke(WitConstants.CANCEL_ERROR);
                throw new TaskCanceledException(WitConstants.CANCEL_ERROR);
            }
        }

        /// <summary>
        /// Causes a task to timeout after a given amount of time in ms. The task's result will be an error message
        /// </summary>
        /// <param name="task">The task to wait for</param>
        /// <param name="ms">The amount of time to wait</param>
        /// <returns>Returns true if this task ran to completion without timing out</returns>
        /// <exception cref="AggregateException"></exception>
        public static async Task<bool> TimeoutAfter(this Task task, int ms)
        {
            var timedOut = false;
            var completedTask = await Task.WhenAny(task, Task.Delay(ms));

            if (task != completedTask)
            {
                timedOut = true;
            }
            else if (null != task.Exception)
            {
                VLog.E($"Task threw an exception while waiting for timeout: {task.Exception.Message}", task.Exception);
                throw task.Exception;
            }
            return !timedOut;
        }

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
