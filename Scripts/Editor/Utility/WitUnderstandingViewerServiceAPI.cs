/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Facebook.WitAi.Events;
using UnityEngine;

namespace Facebook.WitAi.Windows
{
    public abstract class WitUnderstandingViewerServiceAPI
    {
        protected bool HasVoiceActivation;
        protected bool HasTextActivation;
        
        public abstract string ServiceName { get; }

        public abstract bool Active { get; }

        public abstract VoiceEvents Events { get; }
        
        public abstract void Activate();

        public abstract void Activate(string text);

        public abstract void Deactivate();

        public abstract void DeactivateAndAbortRequest();
    }
}
