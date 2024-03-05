/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using Meta.WitAi;
using UnityEngine;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A simple abstract class that can be used to simplify the code
    /// required for a custom IAudioClipStream
    /// </summary>
    public abstract class AudioClipStream : IAudioClipStream
    {
        /// <summary>
        /// Whether or not the stream is ready for playback
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Whether or not the stream has been completed
        /// </summary>
        public bool IsComplete { get; private set; }

        /// <summary>
        /// The current number of channels in this audio data stream
        /// </summary>
        public int Channels { get; protected set; }
        /// <summary>
        /// A getter for the current sample rate of how many samples per second should be played
        /// </summary>
        public int SampleRate { get; protected set; }

        /// <summary>
        /// The total number of samples currently added to this stream.
        /// </summary>
        public int AddedSamples { get; protected set; }
        /// <summary>
        /// The total number of samples expected from the stream.  When streaming, this is not set until the stream is complete.
        /// </summary>
        public int ExpectedSamples { get; protected set; }
        /// <summary>
        /// The maximum known total of samples currently in this stream.
        /// </summary>
        public int TotalSamples => Mathf.Max(AddedSamples, ExpectedSamples);

        /// <summary>
        /// The length of the stream in seconds.
        /// </summary>
        public float Length => GetSampleLength(TotalSamples);

        /// <summary>
        /// A getter for the minimum length in seconds required before the OnStreamReady method is called
        /// </summary>
        public float StreamReadyLength { get; private set; }

        /// <summary>
        /// The callback delegate for stream completion once SetContentLength is called & all samples
        /// have been added via the AddSamples(float[] samples) method.
        /// </summary>
        public AudioClipStreamDelegate OnStreamReady { get; set; }

        /// <summary>
        /// The callback delegate for stream update if any additional data such as the AudioClip is
        /// expected to update mid stream.  This can be set externally but should only be called within
        /// the clip stream itself.
        /// </summary>
        public AudioClipStreamDelegate OnStreamUpdated { get; set; }

        /// <summary>
        /// The callback delegate for stream completion once SetContentLength is called & all samples
        /// have been added via the AddSamples(float[] samples) method.
        /// </summary>
        public AudioClipStreamDelegate OnStreamComplete { get; set; }

        /// <summary>
        /// The constructor that takes in a total channel & sample rate
        /// </summary>
        /// <param name="newChannels">The channels to be used for streaming</param>
        /// <param name="newSampleRate">The new sample rate</param>
        /// <param name="newStreamReadyLength">The minimum length in seconds required before the OnStreamReady method is called</param>
        protected AudioClipStream(int newChannels, int newSampleRate, float newStreamReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH)
        {
            // Set parameters
            Channels = newChannels;
            SampleRate = newSampleRate;
            StreamReadyLength = newStreamReadyLength;

            // Clear counts & bools
            AddedSamples = 0;
            ExpectedSamples = 0;
            IsReady = false;
            IsComplete = false;
        }

        /// <summary>
        /// Adds an array of samples to the current stream
        /// </summary>
        /// <param name="samples">A list of decoded floats from 0f to 1f</param>
        public virtual void AddSamples(float[] samples)
        {
            AddedSamples += samples.Length;
            UpdateState();
        }

        /// <summary>
        /// Calls on occassions where the total samples are known.  Either prior to a disk load or
        /// following a stream completion.
        /// </summary>
        /// <param name="expectedSamples">The final number of samples expected to be received</param>
        public virtual void SetExpectedSamples(int expectedSamples)
        {
            ExpectedSamples = expectedSamples;
            StreamReadyLength = Mathf.Min(StreamReadyLength, GetSampleLength(expectedSamples));
            UpdateState();
        }

        /// <summary>
        /// Calls to determine if completion method should be called
        /// </summary>
        public virtual void UpdateState()
        {
            // Stream ready
            if (!IsReady && (StreamReadyLength <= 0f || GetSampleLength(AddedSamples) >= StreamReadyLength))
            {
                HandleStreamReady();
            }
            // Stream complete
            if (!IsComplete && ExpectedSamples > 0 && AddedSamples >= ExpectedSamples)
            {
                HandleStreamComplete();
            }
        }

        /// <summary>
        /// Perform on stream updated invocation
        /// </summary>
        private void HandleStreamReady()
        {
            if (IsReady)
            {
                return;
            }
            IsReady = true;
            ThreadUtility.CallOnMainThread(RaiseStreamReady);
        }

        /// <summary>
        /// Method that calls OnStreamReady on the main thread
        /// </summary>
        protected virtual void RaiseStreamReady()
        {
            OnStreamReady?.Invoke(this);
        }

        /// <summary>
        /// Perform on stream update following ready
        /// </summary>
        protected virtual void HandleStreamUpdated()
        {
            if (!IsReady)
            {
                return;
            }
            ThreadUtility.CallOnMainThread(RaiseStreamUpdated);
        }

        /// <summary>
        /// Method that calls OnStreamUpdated on the main thread
        /// </summary>
        protected virtual void RaiseStreamUpdated()
        {
            OnStreamUpdated?.Invoke(this);
        }

        /// <summary>
        /// Perform on stream complete invocation
        /// </summary>
        private void HandleStreamComplete()
        {
            if (IsComplete)
            {
                return;
            }
            IsComplete = true;
            ThreadUtility.CallOnMainThread(RaiseStreamComplete);
        }

        /// <summary>
        /// Method that calls OnStreamComplete on the main thread
        /// </summary>
        protected virtual void RaiseStreamComplete()
        {
            OnStreamComplete?.Invoke(this);
        }

        /// <summary>
        /// Called when clip stream should be completely removed from ram
        /// </summary>
        public virtual void Unload()
        {
            OnStreamReady = null;
            OnStreamUpdated = null;
            OnStreamComplete = null;
        }

        /// <summary>
        /// Calculates length in seconds for a specified number of samples
        /// </summary>
        private float GetSampleLength(int totalSamples)
        {
            return GetLength(totalSamples, Channels, SampleRate);
        }

        /// <summary>
        /// Calculates length in seconds based on total samples, channels & samples per second
        /// </summary>
        public static float GetLength(int totalSamples, int channels, int samplesPerSecond)
        {
            return (float) totalSamples / (channels * samplesPerSecond);
        }
    }
}
