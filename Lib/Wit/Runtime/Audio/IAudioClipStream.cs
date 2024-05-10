/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.Voice.Audio
{
    /// <summary>
    /// A delegate for events that provide an IAudioClipStream as a parameter
    /// </summary>
    public delegate void AudioClipStreamDelegate(IAudioClipStream clipStream);

    /// <summary>
    /// A delegate for events that provide an IAudioClipStream as a parameter
    /// </summary>
    public delegate void AudioClipStreamSampleDelegate(float[] samples, int offset, int length);

    /// <summary>
    /// An interface to be used for audio clip streams.  Samples added to this stream
    /// are always decoded to 0f - 1f.
    /// </summary>
    public interface IAudioClipStream
    {
        /// <summary>
        /// Whether or not the stream is ready for playback
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Whether or not the stream has been completed by receiving the total sample
        /// count and applying all samples.
        /// </summary>
        bool IsComplete { get; }

        /// <summary>
        /// A getter for the current number of channels in this audio data stream
        /// </summary>
        int Channels { get; }

        /// <summary>
        /// A getter for the current sample rate of how many samples per second should be played
        /// </summary>
        int SampleRate { get; }

        /// <summary>
        /// The total number of samples currently added to this stream
        /// </summary>
        int AddedSamples { get; }

        /// <summary>
        /// The total number of samples expected from the stream.  When streaming, this is not set until the stream is complete.
        /// </summary>
        int ExpectedSamples { get; }

        /// <summary>
        /// The maximum known total of samples currently in this stream.  Typically Max(AddedSamples, ExpectedSamples)
        /// </summary>
        int TotalSamples { get; }

        /// <summary>
        /// The known length of the stream in seconds.  Typically TotalSamples / (Channels * SampleRate)
        /// </summary>
        float Length { get; }

        /// <summary>
        /// A getter for the minimum length in seconds required before the stream is considered ready for playback.
        /// </summary>
        float StreamReadyLength { get; }

        /// <summary>
        /// The callback delegate for stream samples added.
        /// </summary>
        AudioClipStreamSampleDelegate OnAddSamples { get; set; }

        /// <summary>
        /// The callback delegate for stream ready for playback.  This can be set externally but should only be called within
        /// the clip stream itself.
        /// </summary>
        AudioClipStreamDelegate OnStreamReady { get; set; }

        /// <summary>
        /// The callback delegate for stream update if any additional data such as the AudioClip is
        /// expected to update mid stream.  This can be set externally but should only be called within
        /// the clip stream itself.
        /// </summary>
        AudioClipStreamDelegate OnStreamUpdated { get; set; }

        /// <summary>
        /// The callback delegate for stream completion once SetContentLength is called & all samples
        /// have been added via the AddSamples(float[] samples) method.  This can be set externally but
        /// should only be called within the clip stream itself.
        /// </summary>
        AudioClipStreamDelegate OnStreamComplete { get; set; }

        /// <summary>
        /// The callback when the stream has unloaded all data
        /// </summary>
        AudioClipStreamDelegate OnStreamUnloaded { get; set; }

        /// <summary>
        /// Adds an array of samples to the current stream in its entirety.
        /// </summary>
        /// <param name="samples">A list of decoded floats from 0f to 1f that will be used in their entirety</param>
        void AddSamples(float[] samples);

        /// <summary>
        /// Adds an array of samples to the current stream using a specified offset & length.
        /// </summary>
        /// <param name="samples">A list of decoded floats from 0f to 1f</param>
        /// <param name="offset">The index of samples to begin adding from</param>
        /// <param name="length">The total number of samples that should be added</param>
        void AddSamples(float[] samples, int offset, int length);

        /// <summary>
        /// Calls on occasions where the total samples are known.  Either prior to a disk load or
        /// following a stream completion.
        /// </summary>
        /// <param name="expectedSamples">The final number of samples expected to be received</param>
        void SetExpectedSamples(int expectedSamples);

        /// <summary>
        /// Called when clip stream should be completely removed from RAM
        /// </summary>
        void Unload();
    }
}
