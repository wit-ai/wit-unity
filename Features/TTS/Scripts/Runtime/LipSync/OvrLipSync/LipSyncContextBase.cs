/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEngine;

namespace Meta.WitAi.TTS.LipSync.OvrLipSyncIntegration
{
    //-------------------------------------------------------------------------------------
    // ***** OVRLipSyncContextBase
    //
    /// <summary>
    /// OVRLipSyncContextBase interfaces into the Oculus phoneme recognizer.
    /// This component should be added into the scene once for each Audio Source.
    ///
    /// </summary>
    public abstract class LipSyncContextBase : MonoBehaviour
    {
        [Tooltip("Which lip sync provider to use for viseme computation.")]
        public OvrLipSyncEngine.ContextProviders provider = OvrLipSyncEngine.ContextProviders.Enhanced;
        [Tooltip("Enable DSP offload on supported Android devices.")]
        public bool enableAcceleration = true;

        /// <summary>
        /// Get the current time/elapsed samples of the audio source
        /// </summary>
        public abstract float Time { get; }

        /// <summary>
        /// Is the audio source currently playing
        /// </summary>
        public abstract bool IsPlaying { get; }


        // * * * * * * * * * * * * *
        // Private members
        private OvrLipSyncEngine.Frame frame = new OvrLipSyncEngine.Frame();
        private uint context = 0; // 0 is no context

        private int _smoothing;
        public int Smoothing
        {
            set
            {
                OvrLipSyncEngine.Result result =
                    OvrLipSyncEngine.SendSignal(context, OvrLipSyncEngine.Signals.VisemeSmoothing, value, 0);

                if (result != OvrLipSyncEngine.Result.Success)
                {
                    if (result == OvrLipSyncEngine.Result.InvalidParam)
                    {
                        Debug.LogError("OVRLipSyncContextBase.SetSmoothing: A viseme smoothing" +
                            " parameter is invalid, it should be between 1 and 100!");
                    }
                    else
                    {
                        Debug.LogError("OVRLipSyncContextBase.SetSmoothing: An unexpected" +
                            " error occured.");
                    }
                }

                _smoothing = value;
            }
            get
            {
                return _smoothing;
            }
        }

        public uint Context
        {
            get
            {
                return context;
            }
        }

        protected OvrLipSyncEngine.Frame Frame
        {
            get
            {
                return frame;
            }
        }

        /// <summary>
        /// Awake this instance.
        /// </summary>
        void Awake()
        {
            lock (this)
            {
                if (context == 0)
                {
                    if (OvrLipSyncEngine.CreateContext(ref context, provider, 0, enableAcceleration)
                        != OvrLipSyncEngine.Result.Success)
                    {
                        Debug.LogError("OVRLipSyncContextBase.Start ERROR: Could not create" +
                            " Phoneme context.");
                        return;
                    }
                }
            }
        }


        /// <summary>
        /// Raises the destroy event.
        /// </summary>
        void OnDestroy()
        {
            // Create the context that we will feed into the audio buffer
            lock (this)
            {
                if (context != 0)
                {
                    if (OvrLipSyncEngine.DestroyContext(context) != OvrLipSyncEngine.Result.Success)
                    {
                        Debug.LogError("OVRLipSyncContextBase.OnDestroy ERROR: Could not delete" +
                            " Phoneme context.");
                    }
                }
            }
        }

        // * * * * * * * * * * * * *
        // Public Functions

        /// <summary>
        /// Gets the current phoneme frame (lock and copy current frame to caller frame)
        /// </summary>
        /// <returns>error code</returns>
        /// <param name="inFrame">In frame.</param>
        public OvrLipSyncEngine.Frame GetCurrentPhonemeFrame()
        {
            return frame;
        }

        /// <summary>
        /// Sets a given viseme id blend weight to a given amount
        /// </summary>
        /// <param name="viseme">Integer viseme ID</param>
        /// <param name="amount">Integer viseme amount</param>
        public void SetVisemeBlend(int viseme, int amount)
        {
            OvrLipSyncEngine.Result result =
                OvrLipSyncEngine.SendSignal(context, OvrLipSyncEngine.Signals.VisemeAmount, viseme, amount);

            if (result != OvrLipSyncEngine.Result.Success)
            {
                if (result == OvrLipSyncEngine.Result.InvalidParam)
                {
                    Debug.LogError("OVRLipSyncContextBase.SetVisemeBlend: Viseme ID is invalid.");
                }
                else
                {
                    Debug.LogError("OVRLipSyncContextBase.SetVisemeBlend: An unexpected" +
                        " error occured.");
                }
            }
        }

        /// <summary>
        /// Sets a given viseme id blend weight to a given amount
        /// </summary>
        /// <param name="amount">Integer viseme amount</param>
        public void SetLaughterBlend(int amount)
        {
            OvrLipSyncEngine.Result result =
                OvrLipSyncEngine.SendSignal(context, OvrLipSyncEngine.Signals.LaughterAmount, amount, 0);

            if (result != OvrLipSyncEngine.Result.Success)
            {
                Debug.LogError("OVRLipSyncContextBase.SetLaughterBlend: An unexpected" +
                    " error occured.");
            }
        }

        /// <summary>
        /// Resets the context.
        /// </summary>
        /// <returns>error code</returns>
        public OvrLipSyncEngine.Result ResetContext()
        {
            // Reset visemes to silence etc.
            frame.Reset();

            return OvrLipSyncEngine.ResetContext(context);
        }
    }
}
