/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;

namespace Meta.WitAi
{
  public static class WitConstants
  {
    // Wit service version info
    public const string API_VERSION = "20250213";
    public const string SDK_VERSION = "77.0.0";
    public const string CLIENT_NAME = "wit-unity";

    // Wit service endpoint info
    public const string URI_SCHEME = "https";
    public const string URI_AUTHORITY = "api.wit.ai";
    public const string URI_GRAPH_AUTHORITY = "graph.wit.ai/myprofile";

    public const int URI_DEFAULT_PORT = -1;

    // Default request settings
    public const WitRequestType DEFAULT_REQUEST_TYPE = WitRequestType.Http;
    public const int DEFAULT_REQUEST_TIMEOUT = 10_000;

    // Wit service header keys
    public const string HEADER_REQUEST_ID = "X-Wit-Client-Request-Id";
    public const string HEADER_OP_ID = "X-Wit-Client-Operation-Id";
    public const string HEADER_CLIENT_USER_ID = "client-user-id";
    public const string HEADER_AUTH = "Authorization";
    public const string HEADER_USERAGENT = "User-Agent";
    public const string HEADER_USERAGENT_CONFID_MISSING = "not-yet-configured";
    public const string HEADER_POST_CONTENT = "Content-Type";
    public const string HEADER_GET_CONTENT = "Accept";
    public const string HEADER_TAG_ID = "tag";
    public const string HEADER_DEBUG = "is_debug";

    // Wit service response keys
    public const string RESPONSE_REQUEST_ID = "client_request_id";
    public const string RESPONSE_CLIENT_USER_ID = "client_user_id";
    public const string RESPONSE_OPERATION_ID = "operation_id";

    // Wit response types
    public const string RESPONSE_TYPE_KEY = "type";
    public const string RESPONSE_TYPE_PARTIAL_TRANSCRIPTION = "PARTIAL_TRANSCRIPTION";
    public const string RESPONSE_TYPE_FINAL_TRANSCRIPTION = "FINAL_TRANSCRIPTION";
    public const string RESPONSE_TYPE_PARTIAL_NLP = "PARTIAL_UNDERSTANDING";
    public const string RESPONSE_TYPE_FINAL_NLP = "FINAL_UNDERSTANDING";
    public const string RESPONSE_TYPE_READY_FOR_AUDIO = "INITIALIZED";
    public const string RESPONSE_TYPE_TTS = "SYNTHESIZE_DATA";
    public const string RESPONSE_TYPE_ERROR = "ERROR";
    public const string RESPONSE_TYPE_ABORTED = "ABORTED";
    public const string RESPONSE_TYPE_END = "END_STREAM";

    // NLP Endpoints
    public const string ENDPOINT_SPEECH = "speech";
    public const string ENDPOINT_JSON_MIME = "application/json";
    public const int ENDPOINT_SPEECH_SAMPLE_RATE = 16000;
    public const string ENDPOINT_MESSAGE = "message";
    public const string ENDPOINT_MESSAGE_PARAM = "q";
    public const string ENDPOINT_JSON_DELIMITER = "\r\n";
    public const string ENDPOINT_ERROR_PARAM = "error";

    /// <see cref="https://wit.ai/docs/http/20240304/#context_link"/>
    public const string ENDPOINT_CONTEXT_PARAM = "context";

    // Errors
    public const string ERROR_REACHABILITY = "Endpoint not reachable";
    public const string ERROR_NO_CONFIG = "No WitConfiguration Set";
    public const string ERROR_NO_CONFIG_TOKEN = "No WitConfiguration Client Token";

    // TTS Endpoint
    public const string ENDPOINT_TTS = "synthesize";
    public const string ENDPOINT_TTS_PARAM = "q";
    public const string ENDPOINT_TTS_EVENTS = "viseme";
    public const string ENDPOINT_TTS_EVENT_EXTENSION = "v";
    public const string ENDPOINT_TTS_NO_CLIP = "No tts clip provided";
    public const string ENDPOINT_TTS_NO_TEXT = "No text provided";
    public const int ENDPOINT_TTS_CHANNELS = 1;
    public const int ENDPOINT_TTS_SAMPLE_RATE = 24_000;
    public const float ENDPOINT_TTS_DEFAULT_READY_LENGTH = 1.5f;
    public const float ENDPOINT_TTS_DEFAULT_MAX_LENGTH = 15f;
    public const int ENDPOINT_TTS_QUEUE_PLAYBACK_TIMEOUT = 180_000; // 3 minutes to queue and playback
    public const int ENDPOINT_TTS_DEFAULT_PRELOAD = 5;

    public const int
      ENDPOINT_TTS_BUFFER_LENGTH =
        ENDPOINT_TTS_CHANNELS * ENDPOINT_TTS_SAMPLE_RATE; // Buffer rate is a single second of audio

    public const int
      ENDPOINT_TTS_DEFAULT_SAMPLE_LENGTH =
        (ENDPOINT_TTS_CHANNELS * ENDPOINT_TTS_SAMPLE_RATE) / 1000 * 30; // Each sample returns max 30ms of audio

    public const int
      ENDPOINT_TTS_ERROR_MAX_LENGTH =
        (ENDPOINT_TTS_CHANNELS * ENDPOINT_TTS_SAMPLE_RATE) / 10; // Assumes error if less than 100ms of audio

    public const int ENDPOINT_TTS_MAX_TEXT_LENGTH = 280;

    public const string ERROR_TTS_CACHE_DOWNLOAD = "Preloaded files cannot be downloaded at runtime."
                                                   + " The file will be streamed instead."
                                                   + " If you wish to download this file at runtime, use the temporary or permanent cache.";

    public const string ERROR_TTS_DECODE = "Data failed to encode";
    public const string ERROR_TTS_NO_SAMPLES = "No audio samples returned";
    public const string ERROR_TTS_NO_EVENTS = "No audio events returned";

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

    public static readonly ResponseErrorCode GENERAL_RESPONSE_ERROR_CODE =
      new() { code = -1, codeString = "unknown-general-error" };

    public static readonly ResponseErrorCode[] KNOWN_ERROR_CODES = new[]
    {
      GENERAL_RESPONSE_ERROR_CODE,
      new ResponseErrorCode { code = 10000, codeString = "already-subscribed" },
      new ResponseErrorCode { code = 10001, codeString = "audio-too-long" },
      new ResponseErrorCode { code = 10002, codeString = "bad-request" },
      new ResponseErrorCode { code = 10003, codeString = "bad-response-param" },
      new ResponseErrorCode { code = 10004, codeString = "client-request-id-invalid" },
      new ResponseErrorCode { code = 10005, codeString = "composer-not-deployed" },
      new ResponseErrorCode { code = 10006, codeString = "llm-usage-disabled" },
      new ResponseErrorCode { code = 10007, codeString = "content-type" },
      new ResponseErrorCode { code = 10008, codeString = "empty-transcription" },
      new ResponseErrorCode { code = 10009, codeString = "json-parse" },
      new ResponseErrorCode { code = 10010, codeString = "missing-response-param" },
      new ResponseErrorCode { code = 10011, codeString = "msg-invalid" },
      new ResponseErrorCode { code = 10012, codeString = "msg-type-not-supported" },
      new ResponseErrorCode { code = 10013, codeString = "no-auth" },
      new ResponseErrorCode { code = 10014, codeString = "no-body" },
      new ResponseErrorCode { code = 10015, codeString = "no-meta-auth" },
      new ResponseErrorCode { code = 10016, codeString = "no-subscription" },
      new ResponseErrorCode { code = 10017, codeString = "not-found" },
      new ResponseErrorCode { code = 10018, codeString = "rate-limit" },
      new ResponseErrorCode { code = 10019, codeString = "text-msg-not-supported" },
      new ResponseErrorCode { code = 10020, codeString = "timeout" },
      new ResponseErrorCode { code = 10021, codeString = "unsupported-content-type" },
      new ResponseErrorCode { code = 10022, codeString = "wit" }
    };

    private static Dictionary<int, ResponseErrorCode> codeToResponseErrorCode = null;
    private static Dictionary<string, ResponseErrorCode> codeStringToResponseErrorCode = null;

    public static ResponseErrorCode GetResponseErrorCode(string codeString)
    {
      ResponseErrorCode code = null;
      if (null == codeStringToResponseErrorCode)
      {
        codeStringToResponseErrorCode = new();
        for (int i = 0; i < KNOWN_ERROR_CODES.Length; i++)
        {
          if (KNOWN_ERROR_CODES[i].codeString == codeString) code = KNOWN_ERROR_CODES[i];
          codeStringToResponseErrorCode[KNOWN_ERROR_CODES[i].codeString] = KNOWN_ERROR_CODES[i];
        }
      }
      else
      {
        codeStringToResponseErrorCode.TryGetValue(codeString, out code);
      }

      return code ?? KNOWN_ERROR_CODES[0];
    }

    public static ResponseErrorCode GetResponseErrorCode(int code)
    {
      ResponseErrorCode responseCode = null;
      if (null == codeToResponseErrorCode)
      {
        codeToResponseErrorCode = new();
        for (int i = 0; i < KNOWN_ERROR_CODES.Length; i++)
        {
          if (KNOWN_ERROR_CODES[i].code == code) responseCode = KNOWN_ERROR_CODES[i];
          codeToResponseErrorCode[KNOWN_ERROR_CODES[i].code] = KNOWN_ERROR_CODES[i];
        }
      }
      else
      {
        codeToResponseErrorCode.TryGetValue(code, out responseCode);
      }

      return responseCode ?? KNOWN_ERROR_CODES[0];
    }

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

    #region TTS

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

    /// <summary>
    /// Clip id used for empty text
    /// </summary>
    public const string TTS_EMPTY_ID = "EMPTY";

    /// <summary>
    /// Default audio type suggested for use
    /// </summary>
    public const TTSWitAudioType TTS_TYPE_DEFAULT = TTSWitAudioType.MPEG;

    #endregion TTS

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

    /// <summary>
    /// Simulated error values
    /// </summary>
    public const int ERROR_CODE_SIMULATED = 500;

    public const string ERROR_RESPONSE_SIMULATED = "Simulated Server Error";

    /// <summary>
    /// Returns a unique identifier using the current unix timestamp
    /// and a randomized guid
    /// </summary>
    public static string GetUniqueId()
      => $"{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}-{System.Guid.NewGuid()}";

    #endregion

    #region WEB SOCKETS

    // Setup constant keys & values
    public const string WIT_SOCKET_URL = "wss://api.wit.ai/composer";
    public const int WIT_SOCKET_CONNECT_TIMEOUT = 2000; // Default connection timeout in ms
    public const int WIT_SOCKET_RECONNECT_ATTEMPTS = -1; // Default is retry infinitely
    public const float WIT_SOCKET_RECONNECT_INTERVAL = 1f; // Default to one retry per second
    public const int WIT_SOCKET_RECONNECT_INTERVAL_MIN = 100; // Minimum interval in ms
    public const string WIT_SOCKET_REQUEST_ID_KEY = RESPONSE_REQUEST_ID;
    public const string WIT_SOCKET_CLIENT_USER_ID_KEY = RESPONSE_CLIENT_USER_ID;
    public const string WIT_SOCKET_OPERATION_ID_KEY = RESPONSE_OPERATION_ID;
    public const string WIT_SOCKET_API_KEY = "api_version";

    public const string WIT_SOCKET_CONTENT_KEY = "content_type";

    // Error handling
    public const int WIT_SOCKET_DISCONNECT_CODE = 499;

    public const string WIT_SOCKET_DISCONNECT_ERROR = "WebSocket disconnected";

    // Authorization request constant keys & values
    public const string WIT_SOCKET_AUTH_TOKEN = "wit_auth_token";
    public const string WIT_SOCKET_AUTH_RESPONSE_KEY = "success";
    public const string WIT_SOCKET_AUTH_RESPONSE_VAL = "true";

    public const string WIT_SOCKET_AUTH_RESPONSE_ERROR = "Authentication denied";

    // Request stream specific data
    public const string WIT_SOCKET_DATA_KEY = "data";
    public const string WIT_SOCKET_ACCEPT_KEY = "accept_header";
    public const string WIT_SOCKET_END_KEY = "end_stream";
    public const string WIT_SOCKET_ABORT_KEY = "abort";
    public const string WIT_SOCKET_TRANSCRIBE_KEY = "transcribe";
    public const string WIT_SOCKET_TRANSCRIBE_MULTIPLE_KEY = "multiple_segments";
    public const string WIT_SOCKET_TRANSCRIBE_IS_FINAL = "end_transcription";
    public const char WIT_SOCKET_PARAM_START = '[';
    public const char WIT_SOCKET_PARAM_END = ']';

    public const char WIT_SOCKET_PARAM_DELIM = ',';

    // Pub/sub data keys
    public const string WIT_SOCKET_EXTERNAL_ENDPOINT_KEY = "external";
    public const string WIT_SOCKET_EXTERNAL_UNKNOWN_CLIENT_USER_KEY = "unknown";
    public const string WIT_SOCKET_PUBSUB_SUBSCRIBE_KEY = "subscribe";
    public const string WIT_SOCKET_PUBSUB_UNSUBSCRIBE_KEY = "unsubscribe";
    public const string WIT_SOCKET_PUBSUB_TOPIC_KEY = "topic";
    public const string WIT_SOCKET_PUBSUB_TOPIC_TRANSCRIPTION_KEY = "_ASR";
    public const string WIT_SOCKET_PUBSUB_TOPIC_COMPOSER_KEY = "_COMP";
    public const string WIT_SOCKET_PUBSUB_PUBLISH_KEY = "publish_topics";
    public const string WIT_SOCKET_PUBSUB_PUBLISH_TRANSCRIPTION_KEY = "1"; //"TRANSCRIPTION";
    public const string WIT_SOCKET_PUBSUB_PUBLISH_COMPOSER_KEY = "2"; //"COMPOSER_RESULT";

    // Request Parameter Keys
    public const string PARAM_OP_ID = "operationId";
    public const string PARAM_REQUEST_ID = "requestID";
    public const string PARAM_N_BEST_INTENTS = "nBestIntents";

    #endregion
  }
}
