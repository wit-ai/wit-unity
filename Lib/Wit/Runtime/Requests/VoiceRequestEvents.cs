/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using UnityEngine.Events;

namespace Meta.Voice
{
    [Serializable]
    public class VoiceRequestEvents<TUnityEvent>
        : IVoiceRequestEvents<TUnityEvent>
        where TUnityEvent : UnityEventBase
    {
        /// <summary>
        /// Called whenever a request state changes
        /// </summary>
        public TUnityEvent OnStateChange { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called on download progress update
        /// </summary>
        public TUnityEvent OnDownloadProgressChange { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called on upload progress update
        /// </summary>
        public TUnityEvent OnUploadProgressChange { get; private set; } = Activator.CreateInstance<TUnityEvent>();

        /// <summary>
        /// Called on initial request generation
        /// </summary>
        public TUnityEvent OnInit { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called following the start of data transmission
        /// </summary>
        public TUnityEvent OnSend { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called following the cancellation of a request
        /// </summary>
        public TUnityEvent OnCancel { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called following an error response from a request
        /// </summary>
        public TUnityEvent OnFailed { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called following a successful request & data parse with results provided
        /// </summary>
        public TUnityEvent OnSuccess { get; private set; } = Activator.CreateInstance<TUnityEvent>();
        /// <summary>
        /// Called following cancellation, failure or success to finalize request.
        /// </summary>
        public TUnityEvent OnComplete { get; private set; } = Activator.CreateInstance<TUnityEvent>();
    }
}
