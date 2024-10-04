/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Lib.Wit.Runtime.Utilities.Logging;
using UnityEngine;
using UnityEngine.Networking;
using Meta.WitAi.Json;
using Meta.Voice.Logging;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Callback delegate for progress updates
    /// </summary>
    internal delegate void VRequestProgressDelegate(float progress);
    /// <summary>
    /// Callback delegate for first server response
    /// </summary>
    internal delegate void VRequestResponseDelegate();
    /// <summary>
    /// Delegate that performs decode
    /// </summary>
    internal delegate Task<TValue> VRequestDecodeDelegate<TValue>(UnityWebRequest request);

    /// <summary>
    /// An interface used to ensure requests do not complete prior to request decode
    /// </summary>
    internal interface IVRequestDownloadDecoder
    {
        /// <summary>
        /// Callback for first response
        /// </summary>
        event VRequestResponseDelegate OnFirstResponse;

        /// <summary>
        /// Callback for every response, used for updating timeout
        /// </summary>
        event VRequestResponseDelegate OnResponse;

        /// <summary>
        /// Callback for download progress
        /// </summary>
        event VRequestProgressDelegate OnProgress;

        /// <summary>
        /// Completion source task
        /// </summary>
        TaskCompletionSource<bool> Completion { get; }
    }

    /// <summary>
    /// Supported methods for VRequest
    /// </summary>
    internal enum VRequestMethod
    {
        Unknown,
        HttpGet,
        HttpPost,
        HttpPut,
        HttpHead
    }

    /// <summary>
    /// Server response struct that returns an error and a decoded value
    /// </summary>
    internal struct VRequestResponse<TValue>
    {
        /// <summary>
        /// The type of the result data
        /// </summary>
        public readonly TValue Value;

        /// <summary>
        /// The error string, if errors occurred. Will be NullOrEmpty otherwise.
        /// </summary>
        public int Code;

        /// <summary>
        /// The error string, if errors occurred. Will be NullOrEmpty otherwise.
        /// </summary>
        public string Error;

        /// <summary>
        /// Constructor with only a value
        /// </summary>
        public VRequestResponse(TValue value) : this(value, (int)HttpStatusCode.OK, string.Empty) { }

        /// <summary>
        /// Constructor with only an error
        /// </summary>
        public VRequestResponse(int code, string error) : this(default(TValue), code, error) { }

        /// <summary>
        /// Constructor with value, code and error
        /// </summary>
        public VRequestResponse(TValue value, int code, string error)
        {
            Value = value;
            Code = code;
            Error = error;
        }
    }

    /// <summary>
    /// Helper class for performing http web requests using UnityWebRequest
    /// </summary>
    [LogCategory(LogCategory.Requests)]
    internal class VRequest : IVRequest, ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Requests);

        #region STATIC
        /// <summary>
        /// Ensures only this many requests can run at one time
        /// </summary>
        public static int MaxConcurrentRequests = 3;
        /// <summary>
        /// All currently running request completion tasks
        /// </summary>
        private static List<Task> _activeRequests = new List<Task>();

        /// <summary>
        /// Async wait method to ensure
        /// </summary>
        private static async Task WaitForTurn(VRequest request)
        {
            // Obtain queue of tasks to be awaited
            List<Task> queue = new List<Task>();

            // Lock active requests so no others can begin until this is queued
            lock (_activeRequests)
            {
                // Get previous
                queue.AddRange(_activeRequests);
                // Add current
                _activeRequests.Add(request.Completion.Task);
            }

            // Wait for less than max tasks to be running
            if (queue.Count >= MaxConcurrentRequests)
            {
                await queue.WhenLessThan(MaxConcurrentRequests);
            }
        }
        #endregion STATIC

        #region INSTANCE
        /// <summary>
        /// Url to be used for the vrequest
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Url parameters to be used for the vrequest
        /// </summary>
        public Dictionary<string, string> UrlParameters { get; set; }

        /// <summary>
        /// The content type to be used
        /// </summary>
        public string ContentType { get; set; }

        /// <summary>
        /// Method the request will be used
        /// </summary>
        public VRequestMethod Method { get; set; }

        /// <summary>
        /// Script used as download handler
        /// </summary>
        public DownloadHandler Downloader { get; set; }

        /// <summary>
        /// Script used as upload handler
        /// </summary>
        public UploadHandler Uploader { get; set; }

        /// <summary>
        /// Callback on request upload progress change
        /// </summary>
        public event VRequestProgressDelegate OnUploadProgress;

        /// <summary>
        /// Callback on request download progress change
        /// </summary>
        public event VRequestProgressDelegate OnDownloadProgress;

        /// <summary>
        /// Timeout in seconds
        /// </summary>
        [Obsolete("Use TimeoutMs instead")]
        public int Timeout
        {
            get => Mathf.CeilToInt(TimeoutMs / 1000f);
            set => TimeoutMs = value * 1000;
        }

        /// <summary>
        /// Timeout in milliseconds
        /// </summary>
        public int TimeoutMs { get; set; } = 5_000;

        /// <summary>
        /// Callback on request first response
        /// </summary>
        public event VRequestResponseDelegate OnFirstResponse;

        /// <summary>
        /// If request is currently queued to run
        /// </summary>
        public bool IsQueued { get; private set; } = false;

        /// <summary>
        /// If request is currently transmitting or receiving data
        /// </summary>
        public bool IsRunning { get; private set; } = false;

        /// <summary>
        /// If request is currently decoding following a response
        /// </summary>
        public bool IsDecoding { get; private set; } = false;

        /// <summary>
        /// If request is queued to run or running
        /// </summary>
        public bool IsPerforming => IsQueued || IsRunning || IsDecoding;

        /// <summary>
        /// If first response has been received
        /// </summary>
        public bool HasFirstResponse { get; private set; } = false;

        /// <summary>
        /// If request has completed queueing and running a request
        /// </summary>
        public bool IsComplete { get; private set; } = false;

        /// <summary>
        /// Completion source task
        /// </summary>
        public TaskCompletionSource<bool> Completion { get; private set; } = new TaskCompletionSource<bool>();

        /// <summary>
        /// Response Code if applicable
        /// </summary>
        public int ResponseCode { get; set; } = 0;
        /// <summary>
        /// Response error
        /// </summary>
        public string ResponseError { get; private set; }

        /// <summary>
        /// Current progress for get requests
        /// </summary>
        public float UploadProgress { get; private set; } = 0f;
        /// <summary>
        /// Current progress for download
        /// </summary>
        public float DownloadProgress { get; private set; } = 0f;

        /// <summary>
        /// Stored for cancellation scenarios
        /// </summary>
        private UnityWebRequest _request;

        /// <summary>
        /// Thread safe access to whether request has completed
        /// </summary>
        private TaskCompletionSource<bool> _unityRequestComplete = new TaskCompletionSource<bool>();

        /// <summary>
        /// Datetime of last response received used for timeout
        /// </summary>
        private DateTime _lastResponseReceivedTime;

        /// <summary>
        /// Resets all data
        /// </summary>
        public virtual void Reset()
        {
            IsComplete = false;
            IsQueued = false;
            IsRunning = false;
            IsDecoding = false;
            HasFirstResponse = false;
            UploadProgress = 0f;
            OnUploadProgress?.Invoke(0f);
            DownloadProgress = 0f;
            OnDownloadProgress?.Invoke(0f);
            ResponseCode = (int)HttpStatusCode.OK;
            ResponseError = string.Empty;
        }

        /// <summary>
        /// Performs a request from the main thread or a background thread
        /// </summary>
        public virtual async Task<VRequestResponse<TValue>> Request<TValue>(VRequestDecodeDelegate<TValue> decoder)
        {
            if (IsPerforming)
            {
                return new VRequestResponse<TValue>(WitConstants.ERROR_CODE_GENERAL, $"Cannot make another VRequest while in progress.\nQueued: {IsQueued}\nRunning: {IsRunning}\nDecoding: {IsDecoding}");
            }
            if (decoder == null)
            {
                return new VRequestResponse<TValue>(WitConstants.ERROR_CODE_GENERAL, "Cannot make a VRequest without a decoder.");
            }
            if (string.IsNullOrEmpty(Url))
            {
                return new VRequestResponse<TValue>(WitConstants.ERROR_CODE_GENERAL, "Cannot make a VRequest without a url.");
            }
            var method = GetMethod();
            if (string.IsNullOrEmpty(method))
            {
                return new VRequestResponse<TValue>(WitConstants.ERROR_CODE_GENERAL, "Cannot make a VRequest without a http method.");
            }
            if (IsComplete)
            {
                return new VRequestResponse<TValue>(ResponseCode, ResponseError);
            }

            // Reset data and begin performing
            Reset();

            // Obtain url and headers
            var uri = GetUri();
            var url = uri.AbsoluteUri;
            var headers = GetHeaders();
            if (!string.IsNullOrEmpty(ContentType))
            {
                headers[WitConstants.HEADER_POST_CONTENT] = ContentType;
            }
            else if (headers.TryGetValue(WitConstants.HEADER_POST_CONTENT, out var contentType))
            {
                ContentType = contentType;
            }
            Logger.Verbose("{0} Request\nUrl: {1}\nRequest Id: {2}",
                method,
                url,
                (headers.ContainsKey(WitConstants.HEADER_REQUEST_ID) ? headers[WitConstants.HEADER_REQUEST_ID] : null) ?? "Null");

            // Await queue
            IsQueued = true;
            await WaitForTurn(this);
            IsQueued = false;
            if (IsComplete)
            {
                return new VRequestResponse<TValue>(ResponseCode, ResponseError);
            }

            // Perform timeout
            _ = WaitForTimeout();

            // Generate request on main thread and await completion
            IsRunning = true;
            await ThreadUtility.CallOnMainThread(() =>
            {
                if (IsComplete)
                {
                    return;
                }

                // Create request
                _request = CreateRequest(url, method, headers);

                // Send request
                var asyncOperation = _request.SendWebRequest();
                if (asyncOperation.isDone || _request.isDone)
                {
                    MarkRequestComplete(asyncOperation);
                }
                else
                {
                    asyncOperation.completed += MarkRequestComplete;
                }
            });
            if (!IsComplete)
            {
                await WaitWhileRunning();
            }
            IsRunning = false;
            if (IsComplete)
            {
                return new VRequestResponse<TValue>(ResponseCode, ResponseError);
            }

            // Decode errors and status code
            IsDecoding = true;
            var responseInfo = await GetError(_request);
            ResponseCode = responseInfo.Item1;
            ResponseError = responseInfo.Item2;
            if (!string.IsNullOrEmpty(ResponseError))
            {
                IsDecoding = false;
                return new VRequestResponse<TValue>(ResponseCode, ResponseError);
            }

            // Decode result
            var decodedResult = await decoder.Invoke(_request);
            IsDecoding = false;

            // Aborted or error during decode
            if (IsComplete)
            {
                return new VRequestResponse<TValue>(ResponseCode, ResponseError);
            }

            // Dispose
            Dispose();

            // Return decoded result
            return new VRequestResponse<TValue>(decodedResult);
        }

        /// <summary>
        /// Obtains uri from the url and url parameters
        /// </summary>
        protected const string FilePrepend = "file://";
        protected virtual Uri GetUri()
        {
            string final = Url;
            if (!HasUriSchema(final))
            {
                final = $"{FilePrepend}{final}";
            }
            if (UrlParameters != null)
            {
                const char start = '?';
                bool skipAnd = false;
                if (!final.Contains(start))
                {
                    final += start;
                    skipAnd = true;
                }
                else if (final.EndsWith(start))
                {
                    skipAnd = true;
                }
                foreach (var key in UrlParameters.Keys)
                {
                    var val = UrlParameters[key];
                    if (string.IsNullOrEmpty(key)
                        || string.IsNullOrEmpty(val))
                    {
                        continue;
                    }
                    if (skipAnd) skipAnd = false;
                    else final += '&';
                    val = UnityWebRequest.EscapeURL(val).Replace("+", "%20");
                    final += $"{key}={val}";
                }
            }
            return new Uri(final);
        }

        /// <summary>
        /// Obtain request method id
        /// </summary>
        protected virtual string GetMethod()
        {
            switch (Method)
            {
                case VRequestMethod.HttpGet:
                    return UnityWebRequest.kHttpVerbGET;
                case VRequestMethod.HttpPost:
                    return UnityWebRequest.kHttpVerbPOST;
                case VRequestMethod.HttpPut:
                    return UnityWebRequest.kHttpVerbPUT;
                case VRequestMethod.HttpHead:
                    return UnityWebRequest.kHttpVerbHEAD;
            }
            return null;
        }

        /// <summary>
        /// Obtains request headers if applicable
        /// </summary>
        protected virtual Dictionary<string, string> GetHeaders() => new Dictionary<string, string>();

        /// <summary>
        /// Safely performs a timeout
        /// </summary>
        private async Task WaitForTimeout()
        {
            // Awaits the timeout in ms
            UpdateLastResponseTime();
            await TaskUtility.WaitForTimeout(TimeoutMs, GetLastResponseTime, Completion.Task);

            // Ignore if complete
            if (IsComplete)
            {
                return;
            }

            // Set timeout
            ResponseCode = WitConstants.ERROR_CODE_TIMEOUT;
            ResponseError = WitConstants.ERROR_RESPONSE_TIMEOUT;
            Cancel();
        }

        /// <summary>
        /// Sets last response time
        /// </summary>
        private void UpdateLastResponseTime()
            => _lastResponseReceivedTime = DateTime.UtcNow;

        /// <summary>
        /// Obtain last response using stored variable
        /// </summary>
        private DateTime GetLastResponseTime() => _lastResponseReceivedTime;

        /// <summary>
        /// Generates UnityWebRequest
        /// </summary>
        protected virtual UnityWebRequest CreateRequest(string url,
            string method,
            Dictionary<string, string> headers)
        {
            // Generate request
            var request = new UnityWebRequest(url, method);

            // Apply all headers
            if (headers != null)
            {
                foreach (var key in headers.Keys)
                {
                    request.SetRequestHeader(key, headers[key]);
                }
            }

            // Set upload handler
            if (Uploader != null)
            {
                request.uploadHandler = Uploader;
                request.disposeUploadHandlerOnDispose = true;
            }

            // Set download handler
            if (Downloader != null)
            {
                request.downloadHandler = Downloader;
                request.disposeDownloadHandlerOnDispose = true;
                if (Downloader is IVRequestDownloadDecoder downloadDecoder)
                {
                    downloadDecoder.OnFirstResponse += RaiseFirstResponse;
                    downloadDecoder.OnResponse += UpdateLastResponseTime;
                    downloadDecoder.OnProgress += UpdateDownloadProgress;
                }
            }

            // Return request
            return request;
        }

        /// <summary>
        /// Method call for async operation completion
        /// </summary>
        private void MarkRequestComplete(AsyncOperation asyncOperation)
        {
            if (_request != null && !IsComplete)
            {
                ResponseCode = (int)_request.responseCode;
                ResponseError = _request.error;
            }
            _unityRequestComplete.TrySetResult(true);
        }

        /// <summary>
        /// Wait while request is running
        /// </summary>
        protected virtual async Task WaitWhileRunning()
        {
            // Await unity request or VRequest completion
            await Task.WhenAny(_unityRequestComplete.Task, Completion.Task);

            // Stop waiting if complete, no request or an error is found
            if (IsComplete || _request == null || !string.IsNullOrEmpty(ResponseError))
            {
                return;
            }

            // If downloader decoder is found, await completion or VRequest completion
            if (Downloader is IVRequestDownloadDecoder downloadDecoder
                && downloadDecoder.Completion != null)
            {
                await Task.WhenAny(downloadDecoder.Completion.Task, Completion.Task);
            }
        }

        /// <summary>
        /// Attempts to obtain any errors
        /// </summary>
        protected virtual async Task<Tuple<int, string>> GetError(UnityWebRequest request)
        {
            // Get the current response code and error
            int code = ResponseCode;
            string error = ResponseError;

            // Null or not complete
            if (request == null || !_unityRequestComplete.Task.IsCompleted)
            {
                code = code != (int)HttpStatusCode.OK ? code : WitConstants.ERROR_CODE_GENERAL;
                error = !string.IsNullOrEmpty(error) ? error : "Request disposed prior to completion";
                return new Tuple<int, string>(code, error);
            }

            // Only continue if error is found and download handler exists
            // in order to attempt to get additional information.
            if (string.IsNullOrEmpty(error)
                || Downloader == null)
            {
                return new Tuple<int, string>(code, error);
            }

            // Get downloaded text if possible
            string downloadedText = await GetDownloadedText(request);
            if (string.IsNullOrEmpty(downloadedText))
            {
                return new Tuple<int, string>(code, error);
            }

            // Append to error
            error = $"{error}\nServer Response: {downloadedText}";

            // Try to get error from download handler text json
            WitResponseNode downloadedNode = JsonConvert.DeserializeToken(downloadedText);
            if (downloadedNode == null)
            {
                return new Tuple<int, string>(code, error);
            }
            WitResponseClass downloadedClass = downloadedNode.AsObject;
            if (!downloadedClass.HasChild(WitConstants.ENDPOINT_ERROR_PARAM))
            {
                return new Tuple<int, string>(code, error);
            }
            var jsonError = downloadedClass[WitConstants.ENDPOINT_ERROR_PARAM].Value;
            if (!string.IsNullOrEmpty(jsonError))
            {
                error = jsonError;
            }
            return new Tuple<int, string>(code, error);
        }

        /// <summary>
        /// Attempt to get text from download handler if possible
        /// </summary>
        private async Task<string> GetDownloadedText(UnityWebRequest request)
        {
            string text = null;
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                // Ignore if null
                var downloadHandler = request?.downloadHandler;
                if (downloadHandler == null)
                {
                    return;
                }
                try
                {
                    // Use raw bytes if audio handler
                    if (downloadHandler is DownloadHandlerAudioClip
                        || downloadHandler is DownloadHandlerFile
                        || downloadHandler is DownloadHandlerBuffer)
                    {
                        var rawBytes = downloadHandler.data;
                        if (rawBytes != null)
                        {
                            text = Encoding.UTF8.GetString(rawBytes);
                            return;
                        }
                    }
                    // Otherwise attempt to use text
                    text = downloadHandler?.text;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "VRequest failed to parse downloaded text via {0}", downloadHandler.GetType().Name);
                }
            });
            return text;
        }

        /// <summary>
        /// Method to cancel a VRequest
        /// </summary>
        public virtual void Cancel()
        {
            // Set response
            if (!IsComplete && string.IsNullOrEmpty(ResponseError))
            {
                ResponseCode = WitConstants.ERROR_CODE_ABORTED;
                ResponseError = WitConstants.CANCEL_ERROR;
            }
            // Abort
            if (_request != null)
            {
                ThreadUtility.CallOnMainThread(Logger, () => _request?.Abort());
            }

            // Dispose and ensure complete
            Dispose();
        }

        /// <summary>
        /// Handles dispose and removal of request from running queue
        /// </summary>
        protected virtual void Dispose()
        {
            // Dispose request
            if (_request != null)
            {
                ThreadUtility.CallOnMainThread(Logger, () =>
                {
                    if (_request != null)
                    {
                        // Dispose handlers
                        _request.uploadHandler?.Dispose();
                        _request.downloadHandler?.Dispose();
                        // Dispose request
                        _request.Dispose();
                        _request = null;
                    }
                });
            }

            // Officially complete
            IsComplete = true;

            // Remove request from active list
            lock (_activeRequests)
            {
                _activeRequests.Remove(Completion.Task);
            }

            // Tasks waiting will immediately continue
            if (!Completion.Task.IsCompleted)
            {
                Completion.SetResult(true);
            }
        }

        /// <summary>
        /// Raise first response callback
        /// </summary>
        protected virtual void RaiseFirstResponse()
        {
            if (HasFirstResponse)
            {
                return;
            }
            HasFirstResponse = true;
            OnFirstResponse?.Invoke();
        }

        /// <summary>
        /// Update download progress
        /// </summary>
        protected virtual void UpdateDownloadProgress(float progress)
        {
            if (DownloadProgress.Equals(progress))
            {
                return;
            }
            DownloadProgress = progress;
            OnDownloadProgress?.Invoke(DownloadProgress);
        }
        #endregion

        #region FILE
        /// <summary>
        /// Performs a header request on a uri
        /// </summary>
        public async Task<VRequestResponse<Dictionary<string, string>>> RequestFileHeaders(string url)
        {
            Url = url;
            Method = VRequestMethod.HttpHead;
            return await Request(DecodeFileHeaders);
        }

        /// <summary>
        /// Method to obtain request headers from file
        /// </summary>
        private async Task<Dictionary<string, string>> DecodeFileHeaders(UnityWebRequest request)
        {
            Dictionary<string, string> results = null;
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                results = request.GetResponseHeaders();
            });
            return results;
        }

        /// <summary>
        /// Performs a get request on a file url
        /// </summary>
        public async Task<VRequestResponse<byte[]>> RequestFile(string url)
        {
            Url = url;
            if (Method == VRequestMethod.Unknown) Method = VRequestMethod.HttpGet;
            if (Downloader == null)
            {
                await ThreadUtility.CallOnMainThread(Logger, () =>
                {
                    Downloader = new DownloadHandlerBuffer();
                });
            }
            return await Request(DecodeFile);
        }

        /// <summary>
        /// Method to obtain request headers from file
        /// </summary>
        private async Task<byte[]> DecodeFile(UnityWebRequest request)
        {
            byte[] data = null;
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                data = request.downloadHandler?.data;
            });
            return data;
        }

        /// <summary>
        /// Performs a download from a specified location to a new location
        /// </summary>
        public async Task<VRequestResponse<bool>> RequestFileDownload(string url,
            string downloadPath)
        {
            // Setup
            Url = url;
            if (Method == VRequestMethod.Unknown) Method = VRequestMethod.HttpGet;

            // Get download manager
            var downloadTempPath = GetTmpDownloadPath(downloadPath);
            try
            {
                if (File.Exists(downloadTempPath))
                {
                    File.Delete(downloadTempPath);
                }
            }
            catch (Exception e)
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL,
                    $"Failed to setup download.\nPath: {downloadPath}\n{e}");
            }
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                Downloader = new DownloadHandlerFile(downloadTempPath);
            });

            // Download
            var results = await Request(DecodeSuccess);
            if (!string.IsNullOrEmpty(results.Error))
            {
                return new VRequestResponse<bool>(results.Code, results.Error);
            }
            if (!File.Exists(downloadTempPath))
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL,
                    $"File not found at download path\nPath: {downloadTempPath}");
            }

            try
            {
                // Move to final location
                File.Copy(downloadTempPath, downloadPath, true);
                return new VRequestResponse<bool>(true);
            }
            catch (Exception e)
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL,
                    $"Failed to finalize download.\nPath: {downloadPath}\n{e}");
            }
        }

        /// <summary>
        /// Returns true if no error
        /// </summary>
        protected Task<bool> DecodeSuccess(UnityWebRequest request)
        {
            return Task.FromResult(true);
        }

        // The temporary file path used for download
        public string GetTmpDownloadPath(string downloadPath) => $"{downloadPath}.tmp";

        /// <summary>
        /// Checks if a file exists at a specified location using async calls
        /// </summary>
        public async Task<VRequestResponse<bool>> RequestFileExists(string url)
        {
            // WebGL & web files, perform a header lookup
            if (IsWebUrl(url))
            {
                var results = await RequestFileHeaders(url);
                var success = string.IsNullOrEmpty(results.Error) && results.Value.Keys.Count > 0;
                return new VRequestResponse<bool>(success, results.Code, results.Error);
            }

            // Within a jar, perform a request
            if (IsJarPath(url))
            {
                // Request received data
                bool exists = false;
                OnFirstResponse = () =>
                {
                    exists = true;
                    Cancel();
                };

                // Request async & cancels on first response
                var results = await RequestFile(url);
                if (!exists && results.Code == (int)HttpStatusCode.OK)
                {
                    exists = true;
                }

                // Return if found
                return new VRequestResponse<bool>(exists);
            }

            // Check file directly
            try
            {
                Url = url;
                bool exists = File.Exists(Url);
                return new VRequestResponse<bool>(exists);
            }
            catch (Exception e)
            {
                return new VRequestResponse<bool>(WitConstants.ERROR_CODE_GENERAL, $"File exists check failed\nUrl: {url}\n{e}");
            }
        }
        // Determines if url is a web path or local path
        private static bool IsWebUrl(string url)
        {
            return Regex.IsMatch(url, "(http:|https:).*");
        }
        // Determines if url is a web path or local path
        private static bool IsJarPath(string url)
        {
            bool result = Regex.IsMatch(url, "(jar:).*");
#if UNITY_ANDROID && UNITY_EDITOR
            // Android editor: simulate jar handling
            result |= url.Contains("StreamingAssets");
#endif
            return result;
        }
        // Determines if url is a web path or local path
        private static bool HasUriSchema(string url)
        {
            return Regex.IsMatch(url, "(http:|https:|jar:|file:).*");
        }
        #endregion FILE

        #region TEXT
        /// <summary>
        /// Performs a text request with an option partial response callback
        /// </summary>
        public async Task<VRequestResponse<string>> RequestText(Action<string> onPartial = null)
        {
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                if (onPartial == null)
                {
                    Downloader = new DownloadHandlerBuffer();
                }
                else
                {
                    Downloader = new TextStreamHandler(new TextStreamHandler.TextStreamResponseDelegate(onPartial));
                }
            });
            return await Request(DecodeText);
        }

        /// <summary>
        /// Decodes text from the request itself
        /// </summary>
        private async Task<string> DecodeText(UnityWebRequest request)
        {
            string text = null;
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                text = request.downloadHandler?.text;
            });
            return text;
        }
        #endregion

        #region JSON
        /// <summary>
        /// Performs a request for text & decodes it into json
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJson<TData>(Action<TData> onPartial = null)
        {
            // Set request header for json
            ContentType = "application/json";

            // Last partial decoder
            bool decoded = false;
            TData lastPartial = default(TData);
            Action<string> decoder = null;
            if (onPartial != null)
            {
                decoder = (partialText) =>
                {
                    var partial = DecodeJson<TData>(partialText);
                    if (partial != null)
                    {
                        decoded = true;
                        lastPartial = partial;
                        onPartial?.Invoke(lastPartial);
                    }
                };
            }

            // Perform a text request
            var result = await RequestText(decoder);

            // Return error
            if (!string.IsNullOrEmpty(result.Error))
            {
                return new VRequestResponse<TData>(result.Code, result.Error);
            }
            // If empty, decode now
            if (!decoded)
            {
                lastPartial = DecodeJson<TData>(result.Value);
            }
            // Decode failed
            if (lastPartial == null)
            {
                return new VRequestResponse<TData>(WitConstants.ERROR_CODE_GENERAL,
                    $"Failed to decode {typeof(TData).Name}\n{result.Value}");
            }

            // Return successful decode
            return new VRequestResponse<TData>(lastPartial);
        }

        /// <summary>
        /// Decodes json into a specified data type
        /// </summary>
        private TData DecodeJson<TData>(string json)
        {
            // Return json if string type
            if (typeof(TData) == typeof(string))
            {
                object result = json;
                return (TData)result;
            }
            // Decode
            return JsonConvert.DeserializeObject<TData>(json, null, true);
        }

        /// <summary>
        /// Perform a json get request with the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonGet<TData>(Action<TData> onPartial = null)
        {
            Method = VRequestMethod.HttpGet;
            return await RequestJson(onPartial);
        }

        /// <summary>
        /// Perform a json get request with the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPost<TData>(Action<TData> onPartial = null)
        {
            Method = VRequestMethod.HttpPost;
            return await RequestJson(onPartial);
        }

        /// <summary>
        /// Perform a json post request with raw data and the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPost<TData>(byte[] postData,
            Action<TData> onPartial = null)
        {
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                Uploader = new UploadHandlerRaw(postData);
            });
            return await RequestJsonPost(onPartial);
        }

        /// <summary>
        /// Perform a json post request with string data and the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPost<TData>(string postText,
            Action<TData> onPartial = null)
        {
            var postData = EncodeText(postText);
            return await RequestJsonPost(postData, onPartial);
        }

        /// <summary>
        /// Perform a json put request with the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPut<TData>(Action<TData> onPartial = null)
        {
            Method = VRequestMethod.HttpPut;
            return await RequestJson(onPartial);
        }

        /// <summary>
        /// Perform a json put request with raw data and the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPut<TData>(byte[] putData,
            Action<TData> onPartial = null)
        {
            await ThreadUtility.CallOnMainThread(Logger, () =>
            {
                Uploader = new UploadHandlerRaw(putData);
            });
            return await RequestJsonPut(onPartial);
        }

        /// <summary>
        /// Perform a json put request with string data and the option for a partial response
        /// </summary>
        public async Task<VRequestResponse<TData>> RequestJsonPut<TData>(string putText,
            Action<TData> onPartial = null)
        {
            var postData = EncodeText(putText);
            return await RequestJsonPut(postData, onPartial);
        }

        // Internal helper method for encoding text
        private static byte[] EncodeText(string text) => Encoding.UTF8.GetBytes(text);
        #endregion
    }
}
