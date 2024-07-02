/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

#if !UNITY_WEBGL || UNITY_EDITOR
#define THREADING_ENABLED
#endif

using System;
using System.Collections;
using System.Threading.Tasks;
using Meta.Voice.Logging;
#if THREADING_ENABLED
using UnityEngine;
using System.Threading;
using System.Collections.Concurrent;
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
        private static Thread _mainThread;
        private static readonly ConcurrentQueue<EarlyTask> _earlyTasks = new ConcurrentQueue<EarlyTask>();

        /// <summary>
        /// Compares current thread to main thread in order
        /// to determine location of running code.
        /// </summary>
        public static bool IsMainThread()
            => Thread.CurrentThread == _mainThread;

        /// <summary>
        /// Tasks called prior to main thread setup
        /// </summary>
        private class EarlyTask
        {
            private Task _task;
            public EarlyTask(Task task)
            {
                _task = task;
            }
            public void Start()
            {
                _task.Start(_mainThreadScheduler);
            }
        }

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
            _mainThread = Thread.CurrentThread;
            _mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
            while (_earlyTasks.TryDequeue(out var task))
            {
                task.Start();
            }
        }

        /// <summary>
        /// Adds task to scheduler or early task queue
        /// </summary>
        private static Task EnqueueMainThreadTask(Task task)
        {
            // Start on the main scheduler
            if (_mainThreadScheduler != null)
            {
                task.Start(_mainThreadScheduler);
                return task;
            }
            // Prior to main thread scheduler setup
            var earlyTask = new EarlyTask(task);
            _earlyTasks.Enqueue(earlyTask);
            #if UNITY_EDITOR
            // Add editor callback
            if (!_editorCallback)
            {
                _editorCallback = true;
                UnityEditor.EditorApplication.update += EditorInit;
            }
            #endif
            return task;
        }

#if UNITY_EDITOR
        // Tracking for editor update callback
        private static bool _editorCallback = false;

        /// <summary>
        /// In editor, ensure main thread scheduler is still created
        /// </summary>
        private static void EditorInit()
        {
            if (_editorCallback)
            {
                _editorCallback = false;
            }
            UnityEditor.EditorApplication.update -= EditorInit;
            Init();
        }
#endif
        #endif

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task CallOnMainThread(Action callback)
            => CallOnMainThread(null, callback);

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task CallOnMainThread(IVLogger logger, Action callback)
        {
#if THREADING_ENABLED
            if (!IsMainThread())
            {
                var task = new Task(() => SafeAction(logger, callback));
                return EnqueueMainThreadTask(task);
            }
#endif
            return Task.FromResult(SafeAction(logger, callback));
        }

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task<T> CallOnMainThread<T>(Func<T> callback)
            => CallOnMainThread(null, callback);

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task<T> CallOnMainThread<T>(IVLogger logger, Func<T> callback)
        {
#if THREADING_ENABLED
            if (!IsMainThread())
            {
                var task = new Task<T>(() => SafeAction(logger, callback));
                return (Task<T>)EnqueueMainThreadTask(task);
            }
#endif
            return Task.FromResult(SafeAction(logger, callback));
        }

        /// <summary>
        /// Calls and awaits an action within a try/catch
        /// </summary>
        private static bool SafeAction(IVLogger logger, Action callback)
        {
            try
            {
                callback();
                return true;
            }
            catch (Exception e)
            {
                if (logger == null)
                {
                    VLog.E(e);
                }
                else
                {
                    logger.Error(e);
                }
                throw;
            }
        }

        /// <summary>
        /// Calls and awaits an action within a try/catch
        /// </summary>
        private static T SafeAction<T>(IVLogger logger, Func<T> callback)
        {
            try
            {
                var result = callback();
                return result;
            }
            catch (Exception e)
            {
                if (logger == null)
                {
                    VLog.E(e);
                }
                else
                {
                    logger.Error(e);
                }
                throw;
            }
        }

        /// <summary>
        /// Calls and awaits a task within a try/catch
        /// </summary>
        private static async Task SafeTask(IVLogger logger, Func<Task> callback)
        {
            try
            {
                await callback();
            }
            catch (Exception e)
            {
                logger.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Calls and awaits a task with a return value within a try/catch
        /// </summary>
        private static async Task<T> SafeTask<T>(IVLogger logger, Func<Task<T>> callback)
        {
            try
            {
                return await callback();
            }
            catch (Exception e)
            {
                logger.Error(e);
                throw;
            }
        }

        /// <summary>
        /// Safely backgrounds an async task if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static Task BackgroundAsync(IVLogger logger, Func<Task> callback)
        {
#if THREADING_ENABLED
            if (IsMainThread())
            {
                return Task.Run(() => SafeTask(logger, callback));
            }
#endif
            return SafeTask(logger, callback);
        }

        /// <summary>
        /// Safely backgrounds an async task if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static Task<T> BackgroundAsync<T>(IVLogger logger, Func<Task<T>> callback)
        {
#if THREADING_ENABLED
            if (IsMainThread())
            {
                return Task.Run(() => SafeTask(logger, callback));
            }
#endif
            return SafeTask(logger, callback);
        }

        /// <summary>
        /// Safely backgrounds a callback if threading is enabled in this build.
        /// </summary>
        /// <param name="logger">The logger that should be used for any unhandled exceptions</param>
        /// <param name="callback">The callback to execute</param>
        public static Task Background(IVLogger logger, Action callback)
        {
#if THREADING_ENABLED
            if (IsMainThread())
            {
                return Task.Run(() => SafeAction(logger, callback));
            }
#endif
            return Task.FromResult(SafeAction(logger, callback));
        }

        /// <summary>
        /// Allows yielding on a coroutine to await for a result from an async task
        /// </summary>
        /// <param name="func"></param>
        /// <param name="result"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerator CoroutineAwait(Func<Task> func)
        {
            var task = func.Invoke();
            while (!task.IsCompleted)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Allows yielding on a coroutine to await for a result from an async task
        /// </summary>
        /// <param name="func"></param>
        /// <param name="result"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static IEnumerator CoroutineAwait<T>(Func<Task<T>> func, Action<T> result)
        {
            var task = func.Invoke();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            result(task.Result);
        }

        /// <summary>
        /// Allows yielding on a coroutine to await for a result from an async task
        /// </summary>
        /// <param name="func"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        public static IEnumerator CoroutineAwait<T, T1>(Func<T1, Task<T>> func, T1 data, Action<T> result)
        {
            var task = func.Invoke(data);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            result(task.Result);
        }

        /// <summary>
        /// Allows yielding on a coroutine to await for a result from an async task
        /// </summary>
        /// <param name="func"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        public static IEnumerator CoroutineAwait<T, T1, T2>(Func<T1, T2, Task<T>> func, T1 data1, T2 data2, Action<T> result)
        {
            var task = func.Invoke(data1, data2);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            result(task.Result);
        }

        /// <summary>
        /// Allows yielding on a coroutine to await for a result from an async task
        /// </summary>
        /// <param name="func"></param>
        /// <param name="data"></param>
        /// <param name="result"></param>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="T1"></typeparam>
        /// <returns></returns>
        public static IEnumerator CoroutineAwait<T, T1, T2, T3>(Func<T1, T2, T3, Task<T>> func, T1 data1, T2 data2, T3 data3, Action<T> result)
        {
            var task = func.Invoke(data1, data2, data3);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            result(task.Result);
        }
    }
}
