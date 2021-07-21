using System;
using System.Collections.Concurrent;
using UnityEditor;

namespace com.facebook.witai.utility
{
    public class EditorForegroundRunner
    {
        private static ConcurrentQueue<Action> foregroundQueue = new ConcurrentQueue<Action>();

        public static void Run(Action action)
        {
            foregroundQueue.Enqueue(action);
            EditorApplication.update += FlushQueue;
        }

        private static void FlushQueue()
        {
            EditorApplication.update -= FlushQueue;
            while (foregroundQueue.Count > 0)
            {
                if (foregroundQueue.TryDequeue(out var action))
                {
                    action.Invoke();
                }
            }
        }
    }
}
