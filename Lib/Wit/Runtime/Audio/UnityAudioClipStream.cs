/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using Meta.WitAi;

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A class for generating and appending to audio clips while streaming
    /// </summary>
    public class UnityAudioClipStream : AudioClipStream, IAudioClipProvider, IAudioClipSetter
    {
        [SerializeField] private bool debug;

        /// <summary>
        /// The audio clip to be used for Unity AudioSource playback
        /// </summary>
        public AudioClip Clip { get; private set; }

        // Whether or not the clip should be edited
        private bool _streamable;
        // The streaming chunk size
        private int _chunkSize;

        /// <summary>
        /// Constructor with specific chunk size
        /// </summary>
        /// <param name="newChannels">The audio channels/tracks for the incoming audio data</param>
        /// <param name="newSampleRate">The sample rate for incoming audio data</param>
        /// <param name="newStreamReadyLength">The minimum length in seconds required before the OnStreamReady method is called</param>
        /// <param name="newChunkSamples">Samples to increase audio clip by</param>
        public UnityAudioClipStream(int newChannels, int newSampleRate, float newStreamReadyLength = WitConstants.ENDPOINT_TTS_DEFAULT_READY_LENGTH, float newChunkLength = WitConstants.ENDPOINT_TTS_DEFAULT_BUFFER_LENGTH)
            : base(newChannels, newSampleRate, newStreamReadyLength)
        {
            _streamable = true;
            _chunkSize = Mathf.CeilToInt(Mathf.Max(newChunkLength, newStreamReadyLength) * newChannels * newSampleRate);
        }

        /// <summary>
        /// Sets an audio clip & disables adding additional samples
        /// </summary>
        /// <param name="newClip">Audio clip to be used for playback</param>
        public bool SetClip(AudioClip newClip)
        {
            _streamable = false;
            Clip = newClip;
            Channels = !Clip ? 0 : Clip.channels;
            SampleRate = !Clip ? 0 : Clip.frequency;
            AddedSamples = !Clip ? 0 : Clip.samples;
            ExpectedSamples = !Clip ? 0 : Clip.samples;
            UpdateState();
            return true;
        }

        /// <summary>
        /// Adds an array of samples to the current stream
        /// </summary>
        /// <param name="samples">A list of decoded floats from 0f to 1f</param>
        /// <param name="offset">The index of samples to begin adding from</param>
        /// <param name="length">The total number of samples that should be appended</param>
        public override void AddSamples(float[] samples, int offset, int length)
        {
            // Cannot add samples to non-streamable clip
            if (!_streamable)
            {
                VLog.E(GetType().ToString(), "Cannot add samples to a non-streamable AudioClip");
                return;
            }

            // Generate initial clip
            if (Clip == null)
            {
                int newMaxSamples = Mathf.Max(_chunkSize,
                    AddedSamples + length);
                UpdateClip(newMaxSamples);
            }
            // Generate larger clip if needed
            else if (AddedSamples + length > ExpectedSamples)
            {
                int newMaxSamples = Mathf.Max(TotalSamples + _chunkSize,
                    AddedSamples + length);
                UpdateClip(newMaxSamples);
            }

            // Append to audio clip
            if (length > 0)
            {
                // Get subsection of array
                if (length != samples.Length || offset > 0)
                {
                    var oldSamples = samples;
                    samples = new float[length];
                    Array.Copy(oldSamples, offset, samples, 0, length);
                }
                // Set samples
                Clip.SetData(samples, AddedSamples);
            }

            // Increment AddedSamples & check for completion
            base.AddSamples(samples, offset, length);
        }

        /// <summary>
        /// Calls on occassions where the total samples are known.  Either prior to a disk load or
        /// following a stream completion.
        /// </summary>
        /// <param name="expectedSamples">The final number of samples expected to be received</param>
        public override void SetExpectedSamples(int expectedSamples)
        {
            // Cannot add samples to non-streamable clip
            if (!_streamable)
            {
                VLog.E(GetType().ToString(), "Cannot set total samples of a non-streamable AudioClip");
                return;
            }

            // Set clip with specific length
            UpdateClip(expectedSamples);

            // Increment expected samples & check for completion
            base.SetExpectedSamples(expectedSamples);
        }

        /// <summary>
        /// Called when clip stream should be completely removed from ram
        /// </summary>
        public override void Unload()
        {
            base.Unload();
            if (Clip != null)
            {
                Clip.DestroySafely();
                Clip = null;
            }
        }

        // Generate audio clip for a specific sample count
        private void UpdateClip(int samples)
        {
            // Cannot update a non-streamable clip
            if (!_streamable)
            {
                return;
            }
            // Already generated
            if (Clip != null && Clip.samples == samples)
            {
                return;
            }

            // Get old clip if applicable
            AudioClip oldClip = Clip;
            int oldClipSamples = oldClip == null ? 0 : oldClip.samples;

            // Generate new clip
            Clip = GetCachedClip(samples, Channels, SampleRate);

            // If previous clip existed, get previous data
            if (oldClip != null)
            {
                // Apply existing data
                int copySamples = Mathf.Min(oldClipSamples, samples);
                float[] oldSamples = new float[copySamples];
                oldClip.GetData(oldSamples, 0);
                Clip.SetData(oldSamples, 0);

                // Invoke clip updated callback
                if(debug) VLog.D($"Clip Stream - Clip Updated\nNew Samples: {samples}\nOld Samples: {oldClipSamples}");

                // Requeue previous clip
                ReuseCachedClip(oldClip);
            }
            else
            {
                if(debug) VLog.D($"Clip Stream - Clip Generated\nSamples: {samples}");
            }

            // Handle update
            HandleStreamUpdated();
        }

        #region CACHING
        // Total clips generated including unloaded
        private static int ClipsGenerated = 0;
        // List of preloaded audio clips
        private static List<AudioClip> Clips = new List<AudioClip>();

        /// <summary>
        /// Method used to preload clips to improve performance at runtime
        /// </summary>
        /// <param name="total">Total clips to preload.  This should be the number of clips that could be running at once</param>
        public static void PreloadCachedClips(int total, int lengthSamples, int channels, int frequency)
        {
            for (int i = 0; i < total; i++)
            {
                GenerateCacheClip(lengthSamples, channels, frequency);
            }
        }
        // Preload a single clip
        private static void GenerateCacheClip(int lengthSamples, int channels, int frequency)
        {
            ClipsGenerated++;
            AudioClip clip = AudioClip.Create($"AudioClip_{ClipsGenerated:000}", lengthSamples, channels, frequency, false);
            Clips.Add(clip);
        }
        // Preload a single clip
        private static AudioClip GetCachedClip(int lengthSamples, int channels, int frequency)
        {
            // Find a matching clip
            int clipIndex = Clips.FindIndex((clip) => DoesClipMatch(clip, lengthSamples, channels, frequency));

            // Generate a clip with the specified size
            if (clipIndex == -1)
            {
                clipIndex = Clips.Count;
                GenerateCacheClip(lengthSamples, channels, frequency);
            }

            // Get clip, remove from preload list & return
            AudioClip result = Clips[clipIndex];
            Clips.RemoveAt(clipIndex);
            return result;
        }
        // Check if clip matches
        private static bool DoesClipMatch(AudioClip clip, int lengthSamples, int channels, int frequency)
        {
            return clip.samples == lengthSamples && clip.channels == channels && clip.frequency == frequency;
        }
        // Reuse clip
        private static void ReuseCachedClip(AudioClip clip)
        {
            Clips.Add(clip);
        }
        /// <summary>
        /// Destroy all cached clips
        /// </summary>
        public static void DestroyCachedClips()
        {
            foreach (var clip in Clips)
            {
                clip.DestroySafely();
            }
            Clips.Clear();
        }
        #endregion
    }
}
