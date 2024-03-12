/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi
{
    public static class WitConstants
    {
        // Wit service version info
        public const string API_VERSION = "20240307";
        public const string SDK_VERSION = "65.0.0";
        public const string CLIENT_NAME = "wit-unity";

        // Wit service endpoint info
        public const string URI_SCHEME = "https";
        public const string URI_AUTHORITY = "api.wit.ai";
        public const string URI_GRAPH_AUTHORITY = "graph.wit.ai/myprofile";

        public const int URI_DEFAULT_PORT = -1;

        // Wit service header keys
        public const string HEADER_REQUEST_ID = "X-Wit-Client-Request-Id";
        public const string HEADER_AUTH = "Authorization";
        public const string HEADER_USERAGENT = "User-Agent";
        public const string HEADER_USERAGENT_CONFID_MISSING = "not-yet-configured";
        public const string HEADER_POST_CONTENT = "Content-Type";
        public const string HEADER_GET_CONTENT = "Accept";

        // Wit response types
        public const string RESPONSE_TYPE_KEY = "type";
        public const string RESPONSE_TYPE_PARTIAL_TRANSCRIPTION = "PARTIAL_TRANSCRIPTION";
        public const string RESPONSE_TYPE_FINAL_TRANSCRIPTION = "FINAL_TRANSCRIPTION";
        public const string RESPONSE_TYPE_PARTIAL_NLP = "PARTIAL_UNDERSTANDING";
        public const string RESPONSE_TYPE_FINAL_NLP = "FINAL_UNDERSTANDING";
        public const string RESPONSE_TYPE_TTS = "SYNTHESIZE_DATA";
        public const string RESPONSE_TYPE_ERROR = "ERROR";
        public const string RESPONSE_TYPE_ABORTED = "ABORTED";
        public const string RESPONSE_TYPE_END = "END_STREAM";

        // NLP Endpoints
        public const string ENDPOINT_SPEECH = "speech";
        public const int ENDPOINT_SPEECH_SAMPLE_RATE = 16000;
        public const string ENDPOINT_MESSAGE = "message";
        public const string ENDPOINT_MESSAGE_PARAM = "q";
        public const string ENDPOINT_JSON_DELIMITER = "\r\n";
        public const string ENDPOINT_ERROR_PARAM = "error";

        // Errors
        public const string ERROR_REACHABILITY = "Endpoint not reachable";
        public const string ERROR_NO_CONFIG = "No WitConfiguration Set";
        public const string ERROR_NO_CONFIG_TOKEN = "No WitConfiguration Client Token";

        // TTS Endpoint
        public const string ENDPOINT_TTS = "synthesize";
        public const string ENDPOINT_TTS_PARAM = "q";
        public const string ENDPOINT_TTS_EVENTS = "viseme";
        public const string ENDPOINT_TTS_NO_CLIP = "No tts clip provided";
        public const string ENDPOINT_TTS_NO_TEXT = "No text provided";
        public const int ENDPOINT_TTS_CHANNELS = 1;
        public const int ENDPOINT_TTS_SAMPLE_RATE = 24000;
        public const float ENDPOINT_TTS_DEFAULT_READY_LENGTH = 2.5f;
        public const float ENDPOINT_TTS_DEFAULT_BUFFER_LENGTH = 10f;
        public const int ENDPOINT_TTS_TIMEOUT = 10000; // In ms
        public const int ENDPOINT_TTS_MAX_TEXT_LENGTH = 280;
        public const string ERROR_TTS_CACHE_DOWNLOAD = "Preloaded files cannot be downloaded at runtime.";
        public const string ERROR_TTS_DECODE = "Data failed to encode";

        // Dictation Endpoint
        public const string ENDPOINT_DICTATION = "dictation";

        // Composer Endpoints
        public const string ENDPOINT_COMPOSER_SPEECH = "converse";
        public const string ENDPOINT_COMPOSER_MESSAGE = "event";

        // Used for error checking
        public const string ERROR_NO_TRANSCRIPTION = "Empty transcription.";

        // Reusable constants
        public const string CANCEL_ERROR = "Cancelled";
        public const string CANCEL_MESSAGE_DEFAULT = "Request was cancelled.";
        public const string CANCEL_MESSAGE_PRE_SEND = "Request cancelled prior to transmission begin";
        public const string CANCEL_MESSAGE_PRE_AUDIO = "Request cancelled prior to audio transmission";

        /// <summary>
        /// Error code thrown when an exception is caught during processing or
        /// some other general error happens that is not an error from the server
        /// </summary>
        public const int ERROR_CODE_GENERAL = -1;

        /// <summary>
        /// Error code returned when no configuration is defined
        /// </summary>
        public const int ERROR_CODE_NO_CONFIGURATION = -2;

        /// <summary>
        /// Error code returned when the client token has not been set in the
        /// Wit configuration.
        /// </summary>
        public const int ERROR_CODE_NO_CLIENT_TOKEN = -3;

        /// <summary>
        /// No data was returned from the server.
        /// </summary>
        public const int ERROR_CODE_NO_DATA_FROM_SERVER = -4;

        /// <summary>
        /// Invalid data was returned from the server.
        /// </summary>
        public const int ERROR_CODE_INVALID_DATA_FROM_SERVER = -5;

        /// <summary>
        /// Request was aborted
        /// </summary>
        public const int ERROR_CODE_ABORTED = -6;

        /// <summary>
        /// Request to the server timed out
        /// </summary>
        public const int ERROR_CODE_TIMEOUT = 14;

        // Wit TTS Settings Nodes
        /// <summary>
        /// /synthesize parameter: The voice id to use when speaking via a voice preset.
        /// </summary>
        public static string TTS_VOICE = "voice";
        /// <summary>
        /// Default voice name used if no voice is provided
        /// </summary>
        public const string TTS_VOICE_DEFAULT = "Charlie";

        /// <summary>
        /// /synthesize parameter: Adjusts the style used when speaking with a tts voice
        /// </summary>
        public static string TTS_STYLE = "style";
        /// <summary>
        /// Default style used if no style is provided
        /// </summary>
        public const string TTS_STYLE_DEFAULT = "default";

        /// <summary>
        /// /synthesize parameter: Adjusts the speed at which a TTS voice will speak
        /// </summary>,
        public static string TTS_SPEED = "speed";
        /// <summary>
        /// Default speed used if no speed is provided
        /// </summary>
        public const int TTS_SPEED_DEFAULT = 100;
        /// <summary>
        /// Minimum speed supported by the endpoint (50%)
        /// </summary>
        public const int TTS_SPEED_MIN = 50;
        /// <summary>
        /// Maximum speed supported by the endpoint (200%)
        /// </summary>
        public const int TTS_SPEED_MAX = 200;

        /// <summary>
        /// /synthesize parameter: Adjusts the pitch at which a TTS voice will speak
        /// </summary>
        public static string TTS_PITCH = "pitch";

        /// <summary>
        /// Default pitch used if no speed is provided (100%)
        /// </summary>
        public const int TTS_PITCH_DEFAULT = 100;
        /// <summary>
        /// Minimum pitch supported by the endpoint (25%)
        /// </summary>
        public const int TTS_PITCH_MIN = 25;
        /// <summary>
        /// Maximum pitch supported by the endpoint (200%)
        /// </summary>
        public const int TTS_PITCH_MAX = 200;

        #region Response Body Runtime
        /// <summary>
        /// The key in the response body for the response transcription
        /// </summary>
        public const string KEY_RESPONSE_TRANSCRIPTION = "text";
        /// <summary>
        /// The key in the response body for the final transcription
        /// </summary>
        public const string KEY_RESPONSE_TRANSCRIPTION_IS_FINAL = "is_final";

        /// <summary>
        /// The key in the response body for the response NLP intents
        /// </summary>
        public const string KEY_RESPONSE_NLP_INTENTS = "intents";
        /// <summary>
        /// The key in the response body for the response NLP entities
        /// </summary>
        public const string KEY_RESPONSE_NLP_ENTITIES = "entities";
        /// <summary>
        /// The key in the response body for the response NLP traits
        /// </summary>
        public const string KEY_RESPONSE_NLP_TRAITS = "traits";

        /// <summary>
        /// Key in the response body for partially built responses
        /// </summary>
        public const string KEY_RESPONSE_PARTIAL = "partial_response";
        /// <summary>
        /// Key in the response body for completed responses
        /// </summary>
        public const string KEY_RESPONSE_FINAL = "response";
        /// <summary>
        /// The key in the response body for the action handling
        /// </summary>
        public const string KEY_RESPONSE_ACTION = "action";
        /// <summary>
        /// The key in the response body for the final response
        /// </summary>
        public const string KEY_RESPONSE_IS_FINAL = "is_final";

        /// <summary>
        /// The key in the response body for the response code
        /// </summary>
        public const string KEY_RESPONSE_CODE = "code";

        /// <summary>
        /// The key in the response body for the response error message
        /// </summary>
        public const string KEY_RESPONSE_ERROR = "error";

        /// <summary>
        /// The server received a request, but there was no transcription detected in the input audio stream.
        /// </summary>
        public const string ERROR_RESPONSE_EMPTY_TRANSCRIPTION = "empty-transcription";

        /// <summary>
        /// The active request timed out
        /// </summary>
        public const string ERROR_RESPONSE_TIMEOUT = "timeout";
        #endregion

        /// <summary>
        /// Returns a unique identifier using the current unix timestamp
        /// and a randomized guid
        /// </summary>
        public static string GetUniqueId()
            => $"{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{System.Guid.NewGuid()}";
    }
}
