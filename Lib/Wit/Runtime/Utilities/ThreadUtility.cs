/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if !UNITY_WEBGL
#define THREADING_ENABLED
#endif

using System;
using Meta.Voice.Logging;
#if THREADING_ENABLED
using UnityEngine;
using System.Threading.Tasks;
#endif

namespace Meta.WitAi
{
    /// <summary>
    /// A static class used for performing callbacks on the main thread
    /// </summary>
    public static class ThreadUtility
    {
        #if THREADING_ENABLED
        /// <summary>
        /// The task scheduler that runs on the main thread
        /// </summary>
        private static TaskScheduler _mainThreadScheduler;

        /// <summary>
        /// Called from main thread in constructor of any scripts that need to
        /// call code on the main thread.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            if (_mainThreadScheduler != null)
            {
                return;
            }
            _mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }
        #endif

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task CallOnMainThread(Action callback)
        {
            #if THREADING_ENABLED

            // Get task for callback
            Task task = new Task(callback);

            // Start on the main scheduler
            if (_mainThreadScheduler != null)
            {
                task.Start(_mainThreadScheduler);
                return task;
            }

            // Start here
            task.Start();

            #else

            // Call immediately
            callback?.Invoke();

            #endif
            return task;
        }

        /// <summary>
        /// Safely backgrounds an async task if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static async Task BackgroundAsync(IVLogger logger, Func<Task> callback)
        {
#if THREADING_ENABLED
            await Task.Run(async () =>
            {
                try
                {
                    await callback();
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            });
#else
            await callback();
#endif
        }

        /// <summary>
        /// Safely backgrounds an async task if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static async Task<T> BackgroundAsync<T>(IVLogger logger, Func<Task<T>> callback)
        {
#if THREADING_ENABLED
            return await Task.Run<T>(async () =>
            {
                try
                {
                    return await callback();
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    throw e;
                }
            });
#else
            return await callback();
#endif
        }

        /// <summary>
        /// Safely backgrounds a callback if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static void Background(IVLogger logger, Action callback)
        {
#if THREADING_ENABLED
            Task.Run(() =>
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    logger.Error(e);
                }
            });
#else
            callback();
#endif
        }
    }
}
