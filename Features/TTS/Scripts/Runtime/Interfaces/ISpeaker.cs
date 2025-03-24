/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections;
using System.Threading.Tasks;
using Meta.Voice.Audio;
using Meta.WitAi.Json;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Utilities;

namespace Meta.WitAi.TTS.Interfaces
{
    public interface ISpeaker
    {
        /// <summary>
        /// Whether a clip is currently playing for this speaker
        /// </summary>
        bool IsSpeaking { get; }

        /// <summary>
        /// Whether a clip is currently paused
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Events related to the playback of TTS
        /// </summary>
        TTSSpeakerEvents Events { get; }

        /// <summary>
        /// The specific voice properties used by this speaker
        /// </summary>
        TTSVoiceSettings VoiceSettings { get; }

        // TODO: Consider moving to another interface
        /// <summary>
        /// The script used to perform audio playback of IAudioClipStreams.
        /// </summary>
        IAudioPlayer AudioPlayer { get; }

        /// <summary>
        /// Load a tts clip using the specified text & playback events.
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        void Speak(string textToSpeak, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text & playback events.
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        void SpeakQueued(string textToSpeak, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text and then waits for the file to load & play.
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        IEnumerator SpeakAsync(string textToSpeak);

        /// <summary>
        /// Load a tts clip using the specified text and then waits for the file to load & play.
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        Task SpeakTask(string textToSpeak);

        /// <summary>
        /// Load a tts clip using the specified text & playback events and waits for the file to load and play
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="textToSpeak">The text to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        Task SpeakTask(string textToSpeak, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified response node & playback events
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken & voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        Task SpeakTask(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified response node & playback events
        /// Cancels all previous clips when loaded & then plays.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken & voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        /// <returns>True if responseNode is decoded successfully</returns>
        bool Speak(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases & playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken & voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        IEnumerator SpeakQueuedAsync(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases & playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="responseNode">Parsed data that includes text to be spoken & voice settings</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        Task SpeakQueuedTask(WitResponseNode responseNode, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases & playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        IEnumerator SpeakQueuedAsync(string[] textsToSpeak, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Load a tts clip using the specified text phrases & playback events and then waits for the files to load &
        /// play.  Adds clip to playback queue and will speak once queue has completed all playback.
        /// </summary>
        /// <param name="textsToSpeak">Multiple texts to be spoken</param>
        /// <param name="playbackEvents">Events to be called for this specific tts playback request</param>
        Task SpeakQueuedTask(string[] textsToSpeak, TTSSpeakerClipEvents playbackEvents);

        /// <summary>
        /// Stops loading & playback immediately
        /// </summary>
        void Stop();

        /// <summary>
        /// Pause any current or future loaded audio playback
        /// </summary>
        void Pause();

        /// <summary>
        /// Resume playback for current and future audio clips
        /// </summary>
        void Resume();

        /// <summary>
        /// Call before sending speech data to warm up TTS system
        /// </summary>
        void PrepareToSpeak();

        /// <summary>
        /// Call at the start of a larger text block to indicate many queued requests will be coming
        /// </summary>
        void StartTextBlock();

        /// <summary>
        /// Call at the end of a larger text block to indicate a block of text is complete.
        /// </summary>
        void EndTextBlock();
    }
}
