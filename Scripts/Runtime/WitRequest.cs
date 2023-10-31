/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using UnityEngine.Networking;

namespace Meta.WitAi
{
    /// <summary>
    /// Manages a single request lifecycle when sending/receiving data from Wit.ai.
    ///
    /// Note: This is not intended to be instantiated directly. Requests should be created with the
    /// WitRequestFactory
    /// </summary>
    public class WitRequest : VoiceServiceRequest
    {
        #region PARAMETERS
        /// <summary>
        /// The wit Configuration to be used with this request
        /// </summary>
        public WitConfiguration Configuration { get; private set; }
        /// <summary>
        /// The request timeout in ms
        /// </summary>
        public int TimeoutMs { get; private set; } = 1000;
        /// <summary>
        /// Encoding settings for audio based requests
        /// </summary>
        public AudioEncoding AudioEncoding { get; set; }
        [Obsolete("Deprecated for AudioEncoding")]
        public AudioEncoding audioEncoding
        {
            get => AudioEncoding;
            set => AudioEncoding = value;
        }

        /// <summary>
        /// Endpoint to be used for this request
        /// </summary>
        public string Path
        {
            get => _path;
            set
            {
                if (_canSetPath)
                {
                    _path = value;
                }
                else
                {
                    VLog.W($"Cannot set WitRequest.Path while after transmission.");
                }
            }
        }
        private string _path;
        private bool _canSetPath = true;

        /// <summary>
        /// Final portion of the endpoint Path
        /// </summary>
        public string Command { get; private set; }
        /// <summary>
        /// Whether a post command should be called
        /// </summary>
        public bool IsPost { get; private set; }
        /// <summary>
        /// Key value pair that is sent as a query param in the Wit.ai uri
        /// </summary>
        [Obsolete("Deprecated for Options.QueryParams")]
        public VoiceServiceRequestOptions.QueryParam[] queryParams
        {
            get
            {
                List<VoiceServiceRequestOptions.QueryParam> results = new List<VoiceServiceRequestOptions.QueryParam>();
                foreach (var key in Options?.QueryParams?.Keys)
                {
                    VoiceServiceRequestOptions.QueryParam p = new VoiceServiceRequestOptions.QueryParam()
                    {
                        key = key,
                        value = Options?.QueryParams[key]
                    };
                    results.Add(p);
                }
                return results.ToArray();
            }
        }

        public byte[] postData;
        public string postContentType;
        public string forcedHttpMethodType = null;
        #endregion PARAMETERS

        #region REQUEST
        /// <summary>
        /// Returns true if the request is being performed
        /// </summary>
        public bool IsRequestStreamActive => IsActive || IsInputStreamReady;
        /// <summary>
        /// Returns true if the response had begun
        /// </summary>
        public bool HasResponseStarted { get; private set; }
        /// <summary>
        /// Returns true if the response had begun
        /// </summary>
        public bool IsInputStreamReady { get; private set; }

        public AudioDurationTracker audioDurationTracker;
        private HttpWebRequest _request;
        private Stream _writeStream;
        private object _streamLock = new object();
        private int _bytesWritten;
        private string _stackTrace;
        private DateTime _requestStartTime;
        private ConcurrentQueue<byte[]> _writeBuffer = new ConcurrentQueue<byte[]>();
        #endregion REQUEST

        #region RESULTS
        /// <summary>
        /// Simply return the Path to be called
        /// </summary>
        public override string ToString() => Path;

        /// <summary>
        /// Last response data parsed
        /// </summary>
        private WitResponseNode _lastResponseData;
        #endregion RESULTS

        #region EVENTS
        /// <summary>
        /// Provides an opportunity to provide custom headers for the request just before it is
        /// executed.
        /// </summary>
        public event OnProvideCustomHeadersEvent onProvideCustomHeaders;
        public delegate Dictionary<string, string> OnProvideCustomHeadersEvent();
        /// <summary>
        /// Callback called when the server is ready to receive data from the WitRequest's input
        /// stream. See WitRequest.Write()
        /// </summary>
        public event Action<WitRequest> onInputStreamReady;
        /// <summary>
        /// Returns the raw string response that was received before converting it to a JSON object.
        ///
        /// NOTE: This response comes back on a different thread. Do not attempt ot set UI control
        /// values or other interactions from this callback. This is intended to be used for demo
        /// and test UI, not for regular use.
        /// </summary>
        public Action<string> onRawResponse;

        /// <summary>
        /// Provides an opportunity to customize the url just before a request executed
        /// </summary>
        [Obsolete("Deprecated for WitVRequest.OnProvideCustomUri")]
        public OnCustomizeUriEvent onCustomizeUri;
        public delegate Uri OnCustomizeUriEvent(UriBuilder uriBuilder);
        /// <summary>
        /// Allows customization of the request before it is sent out.
        ///
        /// Note: This is for devs who are routing requests to their servers
        /// before sending data to Wit.ai. This allows adding any additional
        /// headers, url modifications, or customization of the request.
        /// </summary>
        public static PreSendRequestDelegate onPreSendRequest;
        public delegate void PreSendRequestDelegate(ref Uri src_uri, out Dictionary<string,string> headers);
        /// <summary>
        /// Returns a partial utterance from an in process request
        ///
        /// NOTE: This response comes back on a different thread.
        /// </summary>
        [Obsolete("Deprecated for Events.OnPartialTranscription")]
        public event Action<string> onPartialTranscription;
        /// <summary>
        /// Returns a full utterance from a completed request
        ///
        /// NOTE: This response comes back on a different thread.
        /// </summary>
        [Obsolete("Deprecated for Events.OnFullTranscription")]
        public event Action<string> onFullTranscription;

        /// <summary>
        /// Callback called when a response is received from the server off a partial transcription
        /// </summary>
        [Obsolete("Deprecated for Events.OnPartialResponse")]
        public event Action<WitRequest> onPartialResponse;
        /// <summary>
        /// Callback called when a response is received from the server
        /// </summary>
        [Obsolete("Deprecated for Events.OnComplete")]
        public event Action<WitRequest> onResponse;
        #endregion EVENTS

        #region INITIALIZATION
        /// <summary>
        /// Initialize wit request with configuration & path to endpoint
        /// </summary>
        /// <param name="newConfiguration"></param>
        /// <param name="newOptions"></param>
        /// <param name="newEvents"></param>
        public WitRequest(WitConfiguration newConfiguration, string newPath,
            WitRequestOptions newOptions, VoiceServiceRequestEvents newEvents)
            : base(NLPRequestInputType.Audio, newOptions, newEvents)
        {
            // Set Configuration & path
            Configuration = newConfiguration;
            Path = newPath;

            // Finalize
            _initialized = true;
            SetState(VoiceRequestState.Initialized);
        }
        /// <summary>
        /// Only set state if initialized
        /// </summary>
        private bool _initialized = false;
        protected override void SetState(VoiceRequestState newState)
        {
            if (_initialized)
            {
                base.SetState(newState);
            }
        }

        /// <summary>
        /// Finalize initialization
        /// </summary>
        protected override void OnInit()
        {
            // Determine configuration setting
            TimeoutMs = Configuration == null ? TimeoutMs : Configuration.timeoutMS;

            // Set request settings
            Command = Path.Split('/').First();
            IsPost = WitEndpointConfig.GetEndpointConfig(Configuration).Speech == this.Command
                     || WitEndpointConfig.GetEndpointConfig(Configuration).Dictation == this.Command;

            // Finalize bases
            base.OnInit();
        }
        #endregion INITIALIZATION

        #region AUDIO
        // Handle audio activation
        protected override void HandleAudioActivation()
        {
            SetAudioInputState(VoiceAudioInputState.On);
        }
        // Handle audio deactivation
        protected override void HandleAudioDeactivation()
        {
            // If transmitting,
            if (State == VoiceRequestState.Transmitting)
            {
                CloseRequestStream();
            }
            // Call deactivated
            SetAudioInputState(VoiceAudioInputState.Off);
        }
        #endregion

        #region REQUEST
        // Errors that prevent request submission
        protected override string GetSendError()
        {
            // No configuration found
            if (Configuration == null)
            {
                return "Configuration is not set. Cannot start request.";
            }
            // Cannot start without client access token
            if (string.IsNullOrEmpty(Configuration.GetClientAccessToken()))
            {
                return "Client access token is not defined. Cannot start request.";
            }
            // Cannot perform without input stream delegate
            if (onInputStreamReady == null)
            {
                return "No input stream delegate found";
            }
            // Base
            return base.GetSendError();
        }
        // Simple getter for final uri
        private Uri GetUri()
        {
            // Get query parameters
            Dictionary<string, string> queryParams = new Dictionary<string, string>(Options.QueryParams);

            // Get uri using override
            var uri = WitVRequest.GetWitUri(Configuration, Path, queryParams);
            #pragma warning disable CS0618
            if (onCustomizeUri != null)
            {
                #pragma warning disable CS0618
                uri = onCustomizeUri(new UriBuilder(uri));
            }

            // Return uri
            return uri;
        }
        // Simple getter for final uri
        private Dictionary<string, string> GetHeaders()
        {
            // Get default headers
            Dictionary<string, string> headers = WitVRequest.GetWitHeaders(Configuration, Options?.RequestId, false);

            // Append additional headers
            if (onProvideCustomHeaders != null)
            {
                foreach (OnProvideCustomHeadersEvent e in onProvideCustomHeaders.GetInvocationList())
                {
                    Dictionary<string, string> customHeaders = e();
                    if (customHeaders != null)
                    {
                        foreach (var key in customHeaders.Keys)
                        {
                            headers[key] = customHeaders[key];
                        }
                    }
                }
            }

            // Return headers
            return headers;
        }

        /// <summary>
        /// Start the async request for data from the Wit.ai servers
        /// </summary>
        protected override void HandleSend()
        {
            // Begin
            HasResponseStarted = false;

            // Generate results
            _bytesWritten = 0;
            _requestStartTime = DateTime.UtcNow;
            _stackTrace = "-";

            // Get uri & prevent further path changes
            var uri = GetUri();
            _canSetPath = false;

            // Get headers
            var headers = GetHeaders();

            // Allow overrides
            onPreSendRequest?.Invoke(ref uri, out headers);

            #if UNITY_WEBGL && !UNITY_EDITOR || WEBGL_DEBUG
            StartUnityRequest(uri, headers);
            #else
            #if UNITY_WEBGL && UNITY_EDITOR
            if (IsPost)
            {
                VLog.W("Voice input is not supported in WebGL this functionality is fully enabled at edit time, but may not work at runtime.");
            }
            #endif
            StartThreadedRequest(uri, headers);
            #endif
        }
        #endregion REQUEST

        #region HTTP REQUEST
        /// <summary>
        /// Performs a threaded http request
        /// </summary>
        private void StartThreadedRequest(Uri uri, Dictionary<string, string> headers)
        {
            // Create http web request
            _request = WebRequest.Create(uri.AbsoluteUri) as HttpWebRequest;

            // Off to not wait for a response indefinitely
            _request.KeepAlive = false;

            // Configure request method, content type & chunked
            if (forcedHttpMethodType != null)
            {
                _request.Method = forcedHttpMethodType;
            }
            if (null != postContentType)
            {
                if (forcedHttpMethodType == null) {
                    _request.Method = "POST";
                }
                _request.ContentType = postContentType;
                _request.ContentLength = postData.Length;
            }
            if (IsPost)
            {
                _request.Method = string.IsNullOrEmpty(forcedHttpMethodType) ? "POST" : forcedHttpMethodType;
                _request.ContentType = AudioEncoding.ToString();
                _request.SendChunked = true;
            }

            // Apply user agent
            if (headers.ContainsKey(WitConstants.HEADER_USERAGENT))
            {
                _request.UserAgent = headers[WitConstants.HEADER_USERAGENT];
                headers.Remove(WitConstants.HEADER_USERAGENT);
            }
            // Apply all other headers
            foreach (var key in headers.Keys)
            {
                _request.Headers[key] = headers[key];
            }

            // Apply timeout
            _request.Timeout = TimeoutMs;

            // Begin calling on main thread if needed
            WatchMainThreadCallbacks();

            // Perform http post or put
            if (_request.Method == "POST" || _request.Method == "PUT")
            {
                var getRequestTask = _request.BeginGetRequestStream(HandleWriteStream, _request);
                ThreadPool.RegisterWaitForSingleObject(getRequestTask.AsyncWaitHandle,
                    HandleTimeoutMsTimer, _request, TimeoutMs, true);
            }
            // Move right to response
            else
            {
                StartResponse();
            }
        }

        // Start response
        private void StartResponse()
        {
            if (_request == null)
            {
                HandleFailure(WitConstants.ERROR_CODE_GENERAL, "Request canceled prior to start");
                return;
            }
            var asyncResult = _request.BeginGetResponse(HandleResponse, _request);
            ThreadPool.RegisterWaitForSingleObject(asyncResult.AsyncWaitHandle, HandleTimeoutMsTimer, _request, TimeoutMs, true);
        }

        #region Unity Request (WebGL)
        private void StartUnityRequest(Uri uri, Dictionary<string, string> headers)
        {
            UnityWebRequest request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET);

            if (forcedHttpMethodType != null) {
                request.method = forcedHttpMethodType;
            }

            if (null != postContentType)
            {
                if (forcedHttpMethodType == null)
                {
                    request.method = UnityWebRequest.kHttpVerbPOST;
                }

                request.uploadHandler = new UploadHandlerRaw(postData);
                request.uploadHandler.contentType = postContentType;
            }

            // Configure additional headers
            if (IsPost)
            {
                request.method = string.IsNullOrEmpty(forcedHttpMethodType) ?
                    UnityWebRequest.kHttpVerbPOST : forcedHttpMethodType;
                request.SetRequestHeader("Content-Type", audioEncoding.ToString());
            }

            // Apply all wit headers
            foreach (var header in headers)
            {
                request.SetRequestHeader(header.Key, header.Value);
            }

            request.timeout = TimeoutMs;
            request.downloadHandler = new DownloadHandlerBuffer();

            if (request.method == UnityWebRequest.kHttpVerbPOST || request.method == UnityWebRequest.kHttpVerbPUT)
            {
                throw new NotImplementedException("Not yet implemented.");
            }

            SetState(VoiceRequestState.Transmitting);
            VRequest performer = new VRequest();
            performer.RequestText(request, OnUnityRequestComplete);
        }

        private void OnUnityRequestComplete(string response, string error)
        {
            if (!string.IsNullOrEmpty(error))
            {
                HandleFailure(error);
                return;
            }

            // Began
            HasResponseStarted = true;

            // Decode
            _lastResponseData = WitResponseNode.Parse(response);
            try
            {
                onRawResponse?.Invoke(response);
                if (!string.IsNullOrEmpty(_lastResponseData.GetTranscription()))
                {
                    onFullTranscription?.Invoke(_lastResponseData.GetTranscription());
                }
                ApplyResponseData(_lastResponseData, true);
            }
            catch (Exception e)
            {
                HandleFailure(WitConstants.ERROR_CODE_INVALID_DATA_FROM_SERVER, "Error parsing response: " + e + "\n" + response);
            }

            onResponse?.Invoke(this);
        }
        #endregion

        // Handle timeout callback
        private void HandleTimeoutMsTimer(object state, bool timeout)
        {
            // Ignore false or too late
            if (!timeout)
            {
                return;
            }

            // Get path for request uri
            string path = "";
            if (null != _request?.RequestUri?.PathAndQuery)
            {
                var uriSections = _request.RequestUri.PathAndQuery.Split(new char[] { '?' });
                path = uriSections[0].Substring(1);
            }

            // Get error
            var elapsed = (DateTime.UtcNow - _requestStartTime).TotalMilliseconds;
            var error = $"Request [{path}] timed out after {elapsed:0.00} ms";

            // Call error
            MainThreadCallback(() => HandleFailure(WitConstants.ERROR_CODE_TIMEOUT, error));

            // Clean up the current request if it is still going
            if (null != _request)
            {
                _request.Abort();
            }

            // Close any open stream resources and clean up streaming state flags
            CloseActiveStream();
        }

        // Write stream
        private void HandleWriteStream(IAsyncResult ar)
        {
            try
            {
                // Start response stream
                StartResponse();

                // Get write stream
                var stream = _request.EndGetRequestStream(ar);

                // Got write stream
                _bytesWritten = 0;

                // Immediate post
                if (postData != null && postData.Length > 0)
                {
                    _bytesWritten += postData.Length;
                    stream.Write(postData, 0, postData.Length);
                    stream.Close();
                }
                // Wait for input stream
                else
                {
                    // Request stream is ready to go
                    IsInputStreamReady = true;
                    _writeStream = stream;

                    // Call input stream ready delegate
                    if (onInputStreamReady != null)
                    {
                        MainThreadCallback(() => onInputStreamReady(this));
                    }
                }
            }
            catch (WebException e)
            {
                // Ignore cancelation errors & if error already occured
                if (e.Status == WebExceptionStatus.RequestCanceled
                    || e.Status == WebExceptionStatus.Timeout
                    || StatusCode != 0)
                {
                    return;
                }

                // Write stream error
                MainThreadCallback(() => HandleFailure((int) e.Status, e.ToString()));
            }
            catch (Exception e)
            {
                // Call an error if have not done so yet
                if (StatusCode != 0)
                {
                    return;
                }

                // Non web error occured
                MainThreadCallback(() => HandleFailure(WitConstants.ERROR_CODE_GENERAL, e.ToString()));
            }
        }

        /// <summary>
        /// Write request data to the Wit.ai post's body input stream
        ///
        /// Note: If the stream is not open (IsActive) this will throw an IOException.
        /// Data will be written synchronously. This should not be called from the main thread.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void Write(byte[] data, int offset, int length)
        {
            // Ignore without write stream
            if (!IsInputStreamReady || data == null || length == 0)
            {
                return;
            }

            try
            {
                _writeStream.Write(data, offset, length);
                _bytesWritten += length;
                if (audioDurationTracker != null)
                {
                    audioDurationTracker.AddBytes(length);
                }
            }
            catch (ObjectDisposedException e)
            {
                _writeStream = null;
            }
            catch (Exception e)
            {
                return;
            }

            // Perform a cancellation if still waiting for a post
            if (WaitingForPost())
            {
                MainThreadCallback(() => Cancel("Stream was closed with no data written."));
            }
        }

        // Handles response from server
        private void HandleResponse(IAsyncResult asyncResult)
        {
            // Begin response
            HasResponseStarted = true;
            string stringResponse = "";

            // Status code
            int statusCode = (int)HttpStatusCode.OK;
            string error = null;

            try
            {
                // Get response
                using (var response = _request.EndGetResponse(asyncResult))
                {
                    // Got response
                    HttpWebResponse httpResponse = response as HttpWebResponse;

                    // Apply status & description
                    int newStatus = (int)httpResponse.StatusCode;
                    if (statusCode != newStatus)
                    {
                        statusCode = newStatus;
                        error = httpResponse.StatusDescription;
                    }
                    // Decode stream
                    else
                    {
                        using (var responseStream = httpResponse.GetResponseStream())
                        {
                            using (var responseReader = new StreamReader(responseStream))
                            {
                                stringResponse = responseReader.ReadToEnd();
                                if (!string.IsNullOrEmpty(stringResponse))
                                {
                                    ProcessStringResponses(stringResponse);
                                }
                            }
                        }
                    }
                }
            }
            catch (JSONParseException e)
            {
                statusCode = WitConstants.ERROR_CODE_INVALID_DATA_FROM_SERVER;
                error = $"Server returned invalid data.\n\n{e}";
            }
            catch (WebException e)
            {
                if (e.Status != WebExceptionStatus.RequestCanceled
                    && e.Status != WebExceptionStatus.Timeout)
                {
                    // Apply status & error
                    statusCode = (int) e.Status;
                    error = e.ToString();

                    // Attempt additional parse
                    if (e.Response is HttpWebResponse errorResponse)
                    {
                        statusCode = (int) errorResponse.StatusCode;
                        try
                        {
                            using (var errorStream = errorResponse.GetResponseStream())
                            {
                                if (errorStream != null)
                                {
                                    using (StreamReader errorReader = new StreamReader(errorStream))
                                    {
                                        stringResponse = errorReader.ReadToEnd();
                                        if (!string.IsNullOrEmpty(stringResponse))
                                        {
                                            ProcessStringResponses(stringResponse);
                                        }
                                    }
                                }
                            }
                        }
                        catch (JSONParseException)
                        {
                            // Response wasn't encoded error, ignore it.
                        }
                        catch (Exception)
                        {
                            // We've already caught that there is an error, we'll ignore any errors
                            // reading error response data and use the status/original error for validation
                            _stackTrace = e.StackTrace;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                statusCode = WitConstants.ERROR_CODE_GENERAL;
                error = e.ToString();
            }

            // Close request stream if possible
            CloseRequestStream();

            // Done
            HasResponseStarted = false;

            // Ignore if no longer active
            if (!IsActive)
            {
                return;
            }

            // Final callbacks
            MainThreadCallback(() =>
            {
                // Handle failure
                if (statusCode != (int)HttpStatusCode.OK)
                {
                    HandleFailure(statusCode, error);
                }
                // No response
                else if (ResponseData == null)
                {
                    error = $"Server did not return a valid json response.";
#if UNITY_EDITOR
                    error += $"\nActual Response\n{stringResponse}";
#endif
                    HandleFailure(error);
                }
                // Success
                else
                {
                    if (!ResponseData.GetIsFinal())
                    {
                        ApplyTranscription(Transcription, true);
                    }
                    MakeLastResponseFinal();
                }
            });
        }
        // Process individual piece
        private void ProcessStringResponses(string stringResponse)
        {
            // Split by delimiter
            foreach (var stringPart in stringResponse.Split(new string[]{WitConstants.ENDPOINT_JSON_DELIMITER}, StringSplitOptions.RemoveEmptyEntries))
            {
                ProcessStringResponse(stringPart);
            }
        }
        // Safely handles
        private void ProcessStringResponse(string stringResponse)
        {
            // Call raw response for every received response
            if (!string.IsNullOrEmpty(stringResponse))
            {
                MainThreadCallback(() => onRawResponse?.Invoke(stringResponse));
            }

            // Decode full response
            WitResponseNode responseNode = WitResponseNode.Parse(stringResponse);
            bool isTranscriptionOnly = responseNode.IsTranscriptionOnly();
            bool isFinal = responseNode.GetIsFinal();
            string transcription = responseNode.GetTranscription();
            _lastResponseData = responseNode;

            // Apply on main thread
            MainThreadCallback(() =>
            {
                // Set transcription
                if (!string.IsNullOrEmpty(transcription) && (isTranscriptionOnly || isFinal))
                {
                    ApplyTranscription(transcription, isFinal);
                }

                // Set response
                ApplyResponseData(responseNode, false);
            });
        }
        // On text change callback
        protected override void OnPartialTranscription()
        {
            onPartialTranscription?.Invoke(Transcription);
            base.OnPartialTranscription();
        }
        protected override void OnFullTranscription()
        {
            onFullTranscription?.Invoke(Transcription);
            base.OnFullTranscription();
        }
        // On response data change callback
        protected override void OnPartialResponse()
        {
            onPartialResponse?.Invoke(this);
            base.OnPartialResponse();
        }
        // On full response
        protected override void OnFullResponse()
        {
            base.OnFullResponse();
        }
        // Check if data has been written to post stream while still receiving data
        private bool WaitingForPost()
        {
            return IsPost && _bytesWritten == 0 && StatusCode == 0;
        }
        // Check if any data has been written
        protected override bool HasSentAudio() => IsPost && _bytesWritten > 0;
        // Close active stream & then abort if possible
        private void CloseRequestStream()
        {
            // Cancel due to no audio if not an error
            if (WaitingForPost())
            {
                Cancel("Request was closed with no audio captured.");
            }
            // Close
            else
            {
                CloseActiveStream();
            }
        }
        // Close stream
        private void CloseActiveStream()
        {
            IsInputStreamReady = false;
            lock (_streamLock)
            {
                if (null != _writeStream)
                {
                    try
                    {
                        _writeStream.Close();
                    }
                    catch (Exception e)
                    {
                        VLog.W($"Write Stream - Close Failed\n{e}");
                    }
                    _writeStream = null;
                }
            }
        }

        // Perform a cancellation/abort
        protected override void HandleCancel()
        {
            // Close stream
            CloseActiveStream();

            // Abort request
            if (null != _request)
            {
                _request.Abort();
                _request = null;
            }
        }

        // Add response callback & log for abort
        protected override void OnComplete()
        {
            base.OnComplete();

            // Close write stream if still existing
            if (null != _writeStream)
            {
                CloseActiveStream();
            }
            // Abort request if still existing
            if (null != _request)
            {
                _request.Abort();
                _request = null;
            }

            // Finalize response
            onResponse?.Invoke(this);
            onResponse = null;
        }
        #endregion HTTP REQUEST
    }
}
