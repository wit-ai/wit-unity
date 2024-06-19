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
using System.Collections;
using System.Threading;
using Meta.Voice.Logging;
#if THREADING_ENABLED
using UnityEngine;
using System.Threading.Tasks;
using UnityEditor;
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

        private class Initializer
        {
            private Task _task;

            public Initializer(Task task)
            {
                _task = task;
            }

            public void ExecuteInit()
            {
                #if UNITY_EDITOR
                EditorApplication.update -= ExecuteInit;
                #endif
                ThreadUtility.ExecuteInit();
                _task.Start(_mainThreadScheduler);
            }
        }

        /// <summary>
        /// Called from main thread in constructor of any scripts that need to
        /// call code on the main thread.
        /// </summary>
        [RuntimeInitializeOnLoadMethod]
        private static void Init(Initializer initializer = null)
        {
            #if UNITY_EDITOR
            // Ensure the init happens on the main thread if we are in the editor
            if (null != initializer) EditorApplication.update += initializer.ExecuteInit;
            else EditorApplication.update += ExecuteInit;
            #else
            ExecuteInit();
            #endif
        }

        private static void ExecuteInit()
        {
            #if UNITY_EDITOR
            EditorApplication.update -= ExecuteInit;
            #endif
            if (_mainThreadScheduler != null)
            {
                return;
            }

            _mainThread = Thread.CurrentThread;
            _mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        }
        #endif

        /// <summary>
        /// Compares current thread to main thread in order
        /// to determine location of running code.
        /// </summary>
        public static bool IsMainThread()
            => Thread.CurrentThread == _mainThread;

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task CallOnMainThread(Action callback)
        {
            if (IsMainThread())
            {
                callback?.Invoke();
                return Task.FromResult(true);
            }

            // Get task for callback
            Task task = new Task(callback);
            return StartTask(task);
        }

        /// <summary>
        /// Safely calls an action on the main thread using a scheduler.
        /// </summary>
        /// <param name="callback">The action to be performed on the main thread</param>
        public static Task<T> CallOnMainThread<T>(Func<T> callback)
        {
            if (IsMainThread())
            {
                return Task.FromResult(callback.Invoke());
            }
            // Get task for callback
            Task<T> task = new Task<T>(callback);
            return (Task<T>) StartTask(task);
        }

        private static Task StartTask(Task task)
        {
#if THREADING_ENABLED

            // Start on the main scheduler
            if (_mainThreadScheduler != null)
            {
                task.Start(_mainThreadScheduler);
                return task;
            }

#if UNITY_EDITOR
            // This is the unity editor, we need to make sure the Unity Editor foregrounder
            // has been initialized.
            Init(new Initializer(task));
            return task;
#else       // If we're in a build and we made it this far we don't have a scheduler. We will
            // attempt to execute this anyway with a hope we're already on the main thread, but
            // it may trigger a runtime exception. That exception should flag that something is
            // wrong here and may need investigating.
            task.Start();
            return task;
#endif

#else       // Threading is not enabled, we'll call the method immediately since we're already
            // on the main thread
            task.Start();
            return task;
#endif
        }

        /// <summary>
        /// Calls and awaits an action within a try/catch
        /// </summary>
        private static Task SafeAction(IVLogger logger, Action callback)
        {
            try
            {
                callback();
                return Task.FromResult(true);
            }
            catch (Exception e)
            {
                logger.Error(e);
                return Task.FromResult(false);
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
            return SafeAction(logger, callback);
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
