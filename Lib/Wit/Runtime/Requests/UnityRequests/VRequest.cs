/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Meta.WitAi.Json;
using Meta.Voice.Audio.Decoding;
using Meta.Voice.Logging;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// Simple interface for custom download handlers so VRequest can tell when they are done
    /// </summary>
    public interface IRequestDownloadHandler
    {
        /// <summary>
        /// Method for determining if
        /// </summary>
        bool IsComplete { get; }
    }

    /// <summary>
    /// Interface for custom download handler for streaming callbacks
    /// </summary>
    public interface IVRequestStreamable : IRequestDownloadHandler
    {
        bool IsStreamReady { get; }
        void CleanUp();
    }

    /// <summary>
    /// Class for performing web requests using UnityWebRequest
    /// </summary>
    [LogCategory(LogCategory.Requests)]
    public class VRequest
    {
        /// <summary>
        /// Will only start new requests if there are less than this number
        /// If <= 0, then all requests will run immediately
        /// </summary>
        public static int MaxConcurrentRequests = 3;
        // Currently transmitting requests
        private static int _requestCount = 0;

        private readonly IVLogger _log = LoggerRegistry.Instance.GetLogger();

        // Request progress delegate
        public delegate void RequestProgressDelegate(float progress);
        // Request first response
        public delegate void RequestFirstResponseDelegate();
        // Default request completion delegate
        public delegate void RequestCompleteDelegate<TResult>(TResult result, string error);

        /// <summary>
        /// The URI of the current/last request.
        /// </summary>
        protected Uri uri;

        /// <summary>
        /// The data for PUT and POST requests. This will usually be set by child classes that have unencoded payloads.
        /// </summary>
        protected string payload;

        /// <summary>
        /// A request result wrapper which can return a result and error string.
        /// </summary>
        /// <typeparam name="TResult">the type of the result data</typeparam>
        public struct RequestCompleteResponse<TValue>
        {
            /// <summary>
            /// The type of the result data
            /// </summary>
            public TValue Value;

            /// <summary>
            /// The error string, if errors occurred. Will be NullOrEmpty otherwise.
            /// </summary>
            public string Error;

            /// <summary>
            /// Simple constructor with value and error
            /// </summary>
            /// <param name="value">The value of the response</param>
            /// <param name="error">Any returned error from the request</param>
            public RequestCompleteResponse(TValue value, string error)
            {
                Value = value;
                Error = error;
            }
            public RequestCompleteResponse(TValue value) : this(value, string.Empty) { }
            public RequestCompleteResponse(string error) : this(default(TValue), string.Empty) { }
        }

        #region INSTANCE
        /// <summary>
        /// Timeout in seconds
        /// </summary>
        public int Timeout { get; set; } = 5;

        /// <summary>
        /// If request is currently being performed
        /// </summary>
        public bool IsPerforming { get; private set; } = false;

        /// <summary>
        /// If first response has been received
        /// </summary>
        public bool HasFirstResponse { get; private set; } = false;

        /// <summary>
        /// If the download handler incorporates IVRequestStreamable & IsStreamReady
        /// </summary>
        public bool IsStreamReady { get; private set; } = false;

        /// <summary>
        /// Whether or not the completion delegate has been called
        /// </summary>
        public bool IsComplete { get; private set; } = false;

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

        // Actual request
        private UnityWebRequest _request;
        // Callbacks for progress & completion
        private RequestProgressDelegate _onDownloadProgress;
        private RequestFirstResponseDelegate _onFirstResponse;
        private RequestCompleteDelegate<UnityWebRequest> _onStreamReady;
        private RequestCompleteDelegate<UnityWebRequest> _onComplete;

        // Coroutine running the request
        private CoroutineUtility.CoroutinePerformer _coroutine;

        /// <summary>
        /// A constructor that takes in download progress delegate & first response delegate
        /// </summary>
        /// <param name="onDownloadProgress">The callback for progress related to downloading</param>
        /// <param name="onFirstResponse">The callback for the first response of data from a request</param>
        public VRequest(RequestProgressDelegate onDownloadProgress = null,
            RequestFirstResponseDelegate onFirstResponse = null)
        {
            _onDownloadProgress = onDownloadProgress;
            _onFirstResponse = onFirstResponse;
        }

        // Setup request settings
        protected virtual void Setup(UnityWebRequest unityRequest)
        {
            // Setup
            _request = unityRequest;
            IsPerforming = false;
            HasFirstResponse = false;
            IsStreamReady = false;
            IsComplete = false;
            UploadProgress = 0f;
            DownloadProgress = 0f;
            ResponseError = string.Empty;

            // Add all headers
            Dictionary<string, string> headers = GetHeaders();
            if (headers != null)
            {
                foreach (var key in headers.Keys)
                {
                    _request.SetRequestHeader(key, headers[key]);
                }
            }

            // Use request's timeout value
            _request.timeout = Timeout;

            // Dispose handlers automatically
            _request.disposeUploadHandlerOnDispose = true;
            _request.disposeDownloadHandlerOnDispose = true;

            _log.Verbose("Setup request with URL: {0}", _request.uri);
        }

        /// <summary>
        /// Adds 'file://' to url if no prefix is found
        /// </summary>
        public virtual string CleanUrl(string url) => !HasUriSchema(url) ? $"file://{url}" : url;

        // Override for custom headers
        protected virtual Dictionary<string, string> GetHeaders() => null;

        // Begin request
        protected virtual void Begin()
        {
            IsPerforming = true;
            UploadProgress = 0f;
            DownloadProgress = 0f;
            _onDownloadProgress?.Invoke(DownloadProgress);
            _request?.SendWebRequest();
        }

        // Check for whether request is complete
        protected virtual bool IsRequestComplete()
        {
            // No request
            if (_request == null)
            {
                return true;
            }
            // Request still in progress
            if (!_request.isDone)
            {
                return false;
            }
            // No error & download handler
            if (string.IsNullOrEmpty(_request.error) && _request.downloadHandler != null)
            {
                // For custom download handler scripts (isDone always false)
                if (_request.downloadHandler is DownloadHandlerScript)
                {
                    // If custom handler is not complete, don't stop
                    if (_request.downloadHandler is IRequestDownloadHandler customHandler && !customHandler.IsComplete)
                    {
                        return false;
                    }
                }
                // Download handler not complete
                else if (!_request.downloadHandler.isDone)
                {
                    return false;
                }
            }
            // Complete
            return true;
        }

        // Performs an update & begins the stream if needed
        protected virtual void Update()
        {
            // Waiting to begin
            if (!IsPerforming)
            {
                // Start performing request
                if (MaxConcurrentRequests <= 0 || _requestCount < MaxConcurrentRequests)
                {
                    _requestCount++;
                    Begin();
                }
            }
            // Update progresses
            else if (_request != null)
            {
                // Set upload progress
                float newProgress = _request.uploadProgress;
                if (!UploadProgress.Equals(newProgress))
                {
                    UploadProgress = newProgress;
                }

                // Set download progress
                newProgress = _request.downloadProgress;
                if (!DownloadProgress.Equals(newProgress))
                {
                    DownloadProgress = newProgress;
                    _onDownloadProgress?.Invoke(DownloadProgress);
                }

                // First response received, call delegate
                if (!HasFirstResponse && _request.downloadedBytes > 0)
                {
                    HasFirstResponse = true;
                    _onFirstResponse?.Invoke();
                }

                // Stream is ready, call delegate
                if (!IsStreamReady && _request?.downloadHandler is IVRequestStreamable streamHandler && streamHandler.IsStreamReady)
                {
                    IsStreamReady = true;
                    _onStreamReady?.Invoke(_request, string.Empty);
                }
            }
        }

        // Abort request
        public virtual void Cancel()
        {
            // Cancel
            if (_onComplete != null && _request != null)
            {
                DownloadProgress = 1f;
                ResponseCode = WitConstants.ERROR_CODE_ABORTED;
                ResponseError = WitConstants.CANCEL_ERROR;
                _onDownloadProgress?.Invoke(DownloadProgress);
                _onComplete?.Invoke(_request, ResponseError);
            }

            // Unload
            Unload();
        }

        // Request destroy
        protected virtual void Unload()
        {
            // Cancel coroutine
            if (_coroutine != null)
            {
                _coroutine.CoroutineCancel();
                _coroutine = null;
            }

            // Complete
            if (IsPerforming)
            {
                IsPerforming = false;
                _requestCount--;
            }

            // Remove delegates
            _onDownloadProgress = null;
            _onFirstResponse = null;
            _onStreamReady = null;
            _onComplete = null;

            // Dispose
            if (_request != null)
            {
                // Additional cleanup
                if (_request.downloadHandler is IVRequestStreamable audioStreamer)
                {
                    audioStreamer.CleanUp();
                }
                // Dispose handlers
                _request.uploadHandler?.Dispose();
                _request.downloadHandler?.Dispose();
                // Dispose request
                _request.Dispose();
                _request = null;
            }

            // Officially complete
            IsComplete = true;
        }

        // Returns more specific request error
        public static string GetSpecificRequestError(UnityWebRequest request)
        {
            // Get error & return if empty
            string error = request.error;
            if (string.IsNullOrEmpty(error))
            {
                return error;
            }

            // Ignore without download handler
            if (request.downloadHandler == null)
            {
                return error;
            }

            // Ignore without downloaded json
            string downloadedJson = string.Empty;
            try
            {
                var downloadHandler = request?.downloadHandler;
                byte[] downloadedBytes = downloadHandler?.data;
                if (downloadedBytes != null)
                {
                    downloadedJson = Encoding.UTF8.GetString(downloadedBytes);
                }
                else
                {
                    string downloadedText = downloadHandler?.text;
                    if (!string.IsNullOrEmpty(downloadedText))
                    {
                        downloadedJson = downloadedText;
                    }
                }
            }
            catch (Exception e)
            {
                VLog.W($"VRequest failed to parse downloaded text\n{e}");
            }
            if (string.IsNullOrEmpty(downloadedJson))
            {
                return error;
            }

            // Append json result
            string result = $"{error}\nServer Response: {downloadedJson}";

            // Decode
            WitResponseNode downloadedNode = WitResponseNode.Parse(downloadedJson);
            if (downloadedNode == null)
            {
                return result;
            }

            // Check for error
            WitResponseClass downloadedClass = downloadedNode.AsObject;
            if (!downloadedClass.HasChild(WitConstants.ENDPOINT_ERROR_PARAM))
            {
                return result;
            }

            // Get final result
            return downloadedClass[WitConstants.ENDPOINT_ERROR_PARAM].Value;
        }
        #endregion

        #region COROUTINE
        /// <summary>
        /// Initialize with a request and an on completion callback
        /// </summary>
        /// <param name="unityRequest">The unity request to be performed</param>
        /// <param name="onComplete">The callback on completion, returns the request & error string</param>
        /// <returns>False if the request cannot be performed</returns>
        public virtual bool Request(UnityWebRequest unityRequest,
            RequestCompleteDelegate<UnityWebRequest> onComplete)
        {
            // Already setup
            if (_request != null)
            {
                onComplete?.Invoke(unityRequest, "Request is already being performed");
                return false;
            }

            // Set on complete delegate & setup
            _onComplete = onComplete;
            Setup(unityRequest);

            // Begin coroutine
            _coroutine = CoroutineUtility.StartCoroutine(PerformUpdate());

            // Success
            return true;
        }
        // Perform update
        private IEnumerator PerformUpdate()
        {
            // Update until complete
            while (!IsRequestComplete())
            {
                yield return null;
                Update();
            }

            // Complete if still performing
            if (IsPerforming)
            {
                DownloadProgress = 1f;
                ResponseCode = (int)_request.responseCode;
                ResponseError = GetSpecificRequestError(_request);
                _onDownloadProgress?.Invoke(DownloadProgress);
                _onComplete?.Invoke(_request, ResponseError);
                _onComplete = null;
            }

            // Unload
            Unload();
        }
        #endregion

        #region TASK
        /// <summary>
        /// Initialize with a request and an on completion callback
        /// </summary>
        /// <param name="unityRequest">The unity request to be performed</param>
        /// <param name="onDecode">A function to be performed to async decode all request data</param>
        /// <returns>Any errors encountered during the request</returns>
        public virtual async Task<RequestCompleteResponse<TData>> RequestAsync<TData>(UnityWebRequest unityRequest,
                Func<UnityWebRequest, TData> onDecode)
        {
            // Already setup
            if (_request != null)
            {
                return new RequestCompleteResponse<TData>("Request is already being performed");
            }

            // Setup
            Setup(unityRequest);

            // Continue while request exists & is not complete
            while (!IsRequestComplete())
            {
                await TaskUtility.Wait();
                Update();
            }

            // Complete if still performing
            TData results = default(TData);
            if (IsPerforming && _request != null)
            {
                // Set code & error if applicable
                DownloadProgress = 1f;
                ResponseCode = (int)_request.responseCode;
                ResponseError = GetSpecificRequestError(_request);

                // Decode
                if (onDecode != null && (string.IsNullOrEmpty(ResponseError) || typeof(TData) == typeof(string)))
                {
                    try
                    {
                        results = onDecode.Invoke(_request);
                        if (results == null)
                        {
                            ResponseError = "Decode failed";
                        }
                    }
                    catch (Exception e)
                    {
                        ResponseError = $"Decode failed\n{e}";
                    }
                }

                // Perform callbacks
                _onDownloadProgress?.Invoke(DownloadProgress);
                _onComplete?.Invoke(_request, ResponseError);
                _onComplete = null;
            }

            // Unload
            Unload();

            // Return results
            return new RequestCompleteResponse<TData>(results, ResponseError);
        }
        #endregion

        #region FILE
        /// <summary>
        /// Performs a header request on a uri
        /// </summary>
        /// <param name="uri">The uri to perform the request on</param>
        /// <param name="onComplete">A completion callback that includes the headers</param>
        /// <returns></returns>
        public bool RequestFileHeaders(Uri uri,
            RequestCompleteDelegate<Dictionary<string, string>> onComplete)
        {
            // Header unity request
            UnityWebRequest unityRequest = UnityWebRequest.Head(uri);

            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                // Error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                    return;
                }

                // Headers dictionary if possible
                Dictionary<string, string> headers = response.GetResponseHeaders();
                if (headers == null)
                {
                    onComplete?.Invoke(null, "No headers in response.");
                    return;
                }

                // Success
                onComplete?.Invoke(headers, string.Empty);
            });
        }

        /// <summary>
        /// Performs a header request on a uri asynchronously
        /// </summary>
        /// <param name="uri">The uri to perform the request on</param>
        /// <returns>Returns the header</returns>
        public async Task<RequestCompleteResponse<Dictionary<string, string>>> RequestFileHeadersAsync(Uri uri)
        {
            // Header unity request
            UnityWebRequest unityRequest = UnityWebRequest.Head(uri);

            // Perform request & return the results
            return await RequestAsync(unityRequest, (request) => request.GetResponseHeaders());
        }

        /// <summary>
        /// Performs a simple http header request
        /// </summary>
        /// <param name="uri">Uri to get a file</param>
        /// <param name="onComplete">Called once file data has been loaded</param>
        /// <returns>False if cannot begin request</returns>
        public bool RequestFile(Uri uri,
            RequestCompleteDelegate<byte[]> onComplete)
        {
            // Get unity request
            UnityWebRequest unityRequest = UnityWebRequest.Get(uri);
            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                // Error
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(null, error);
                    return;
                }

                // File data
                byte[] fileData = response?.downloadHandler?.data;
                if (fileData == null)
                {
                    onComplete?.Invoke(null, "No data in response");
                    return;
                }

                // Success
                onComplete?.Invoke(fileData, string.Empty);
            });
        }

        /// <summary>
        /// Performs a simple http header request
        /// </summary>
        /// <param name="uri">Uri to get a file</param>
        /// <returns>False if cannot begin request</returns>
        public async Task<RequestCompleteResponse<byte[]>> RequestFileAsync(Uri uri)
        {
            // Get unity request
            UnityWebRequest unityRequest = UnityWebRequest.Get(uri);

            // Perform request
            return await RequestAsync(unityRequest, (request) =>  unityRequest.downloadHandler?.data);
        }

        /// <summary>
        /// Checks if a file exists at a specified location using async calls
        /// </summary>
        /// <param name="checkPath">The local file path to be checked</param>
        /// <returns>An error if found</returns>
        public async Task<RequestCompleteResponse<bool>> RequestFileExistsAsync(string checkPath)
        {
            // Results
            RequestCompleteResponse<bool> results = new RequestCompleteResponse<bool>();
            results.Value = false;

            // WebGL & web files, perform a header lookup
            if (IsWebUrl(checkPath))
            {
                var headerResponse = await RequestFileHeadersAsync(new Uri(CleanUrl(checkPath)));
                results.Error = headerResponse.Error;
                results.Value = string.IsNullOrEmpty(results.Error) && headerResponse.Value.Keys.Count > 0;
                if (string.IsNullOrEmpty(results.Error) && !results.Value)
                {
                    results.Error = "No headers found";
                }
                return results;
            }

            // Request required
            if (IsJarPath(checkPath))
            {
                // Request received data
                _onFirstResponse = () =>
                {
                    results.Value = true;
                    Cancel();
                };

                // Request complete
                _onComplete = (request, error) =>
                {
                    if (!string.Equals(error, WitConstants.CANCEL_ERROR))
                    {
                        results.Error = error;
                        results.Value = string.IsNullOrEmpty(error);
                    }
                };

                // Request async & cancels on first response
                await RequestFileAsync(new Uri(CleanUrl(checkPath)));

                // Return if found
                return results;
            }

            // Check file directly
            try
            {
                results.Value = File.Exists(checkPath);
            }
            catch (Exception e)
            {
                results.Error = $"File exists check failed\nPath: {checkPath}\n{e}";
            }

            // Success
            return results;
        }

        /// <summary>
        /// Uses async method to check if file exists & return via the oncomplete method
        /// </summary>
        /// <param name="checkPath">The local file path to be checked</param>
        public bool RequestFileExists(string checkPath, RequestCompleteDelegate<bool> onComplete)
        {
            // Request async but don't wait
            #pragma warning disable CS4014
            WaitFileExists(checkPath, onComplete);
            #pragma warning restore CS4014
            return true;
        }
        private async void WaitFileExists(string checkPath, RequestCompleteDelegate<bool> onComplete)
        {
            RequestCompleteResponse<bool> results = await RequestFileExistsAsync(checkPath);
            onComplete?.Invoke(results.Value, results.Error);
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
            result = result || (Application.isPlaying && url.StartsWith(Application.streamingAssetsPath));
#endif
            return result;
        }
        // Determines if url is a web path or local path
        private static bool HasUriSchema(string url)
        {
            return Regex.IsMatch(url, "(http:|https:|jar:|file:).*");
        }
        #endregion

        #region DOWNLOADING
        /// <summary>
        /// Download a file using a unityrequest
        /// </summary>
        /// <param name="unityRequest">The unity request to add a download handler to</param>
        /// <param name="onComplete">Called once download has completed</param>
        public bool RequestFileDownload(string downloadPath, UnityWebRequest unityRequest,
            RequestCompleteDelegate<bool> onComplete)
        {
            // Get temporary path for download
            string tempDownloadPath = GetTmpDownloadPath(downloadPath);

            // Check for setup errors
            string errors = SetupDownloadRequest(tempDownloadPath, unityRequest);
            if (!string.IsNullOrEmpty(errors))
            {
                onComplete?.Invoke(false, errors);
                return false;
            }

            // Perform request
            return Request(unityRequest, (response, error) =>
                CoroutineUtility.StartCoroutine(WaitThenCleanup(downloadPath, tempDownloadPath, onComplete, error)));
        }

        // Wait a moment until request is done unloading & then perform cleanup
        private IEnumerator WaitThenCleanup(string downloadPath, string tempDownloadPath,
            RequestCompleteDelegate<bool> onComplete, string error)
        {
            yield return null;
            string errors = CleanupDownloadRequest(downloadPath, tempDownloadPath, error);
            onComplete?.Invoke(string.IsNullOrEmpty(errors), errors);
        }

        /// <summary>
        /// Download a file using a unityrequest
        /// </summary>
        /// <param name="unityRequest">The unity request to add a download handler to</param>
        public async Task<string> RequestFileDownloadAsync(string downloadPath, UnityWebRequest unityRequest)
        {
            // Get temporary path for download
            string tempDownloadPath = GetTmpDownloadPath(downloadPath);

            // Perform setup
            string errors = SetupDownloadRequest(tempDownloadPath, unityRequest);
            // Return setup errors
            if (!string.IsNullOrEmpty(errors))
            {
                return errors;
            }

            // Perform request
            var response = await RequestAsync(unityRequest, (request) => string.IsNullOrEmpty(request.error));
            errors = response.Error;

            // Cleanup request on background thread due to file move
            await Task.Run(() => errors = CleanupDownloadRequest(downloadPath, tempDownloadPath, errors));

            // Return errors
            return errors;
        }

        // The temporary file path used for download
        private string GetTmpDownloadPath(string downloadPath) => $"{downloadPath}.tmp";

        /// <summary>
        /// Setup file download if possible
        /// </summary>
        /// <returns>Returns any errors encountered during setup</returns>
        private string SetupDownloadRequest(string downloadPath, UnityWebRequest unityRequest)
        {
            // Invalid path
            if (string.IsNullOrEmpty(downloadPath))
            {
                return "Null download path";
            }
            // Ensure valid directory
            if (IsWebUrl(downloadPath) || IsJarPath(downloadPath))
            {
                return $"Cannot download to path:\n{downloadPath}";
            }

            try
            {
                // Check directory
                FileInfo fileInfo = new FileInfo(downloadPath);
                if (!Directory.Exists(fileInfo.DirectoryName))
                {
                    return $"Cannot download to directory\nDirectory: {fileInfo.DirectoryName}";
                }

                // Delete existing file if applicable
                if (File.Exists(downloadPath))
                {
                    File.Delete(downloadPath);
                }
            }
            catch (Exception e)
            {
                return $"{e.GetType()} thrown during download setup\n{e}";
            }

            // Add request handler
            DownloadHandlerFile fileHandler = new DownloadHandlerFile(downloadPath, true);
            unityRequest.downloadHandler = fileHandler;
            unityRequest.disposeDownloadHandlerOnDispose = true;

            // Success
            return string.Empty;
        }

        /// <summary>
        /// Finalize file download if possible
        /// </summary>
        /// <returns>Returns any errors encountered during setup</returns>
        private string CleanupDownloadRequest(string downloadPath, string tempDownloadPath, string error)
        {
            try
            {
                // Handle existing temp file
                if (File.Exists(tempDownloadPath))
                {
                    // For error, remove temp
                    if (!string.IsNullOrEmpty(error))
                    {
                        File.Delete(tempDownloadPath);
                    }
                    // For success, move to final path
                    else
                    {
                        // File already at download path, delete it
                        if (File.Exists(downloadPath))
                        {
                            File.Delete(downloadPath);
                        }

                        // Move to final path
                        File.Move(tempDownloadPath, downloadPath);
                    }
                }
            }
            catch (Exception e)
            {
                return $"{e.GetType()} thrown during download cleanup\n{e}";
            }
            return error;
        }
        #endregion

        #region TEXT
        /// <summary>
        /// Performs a text request & handles the resultant text
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        /// <param name="onComplete">The delegate upon completion</param>
        public bool RequestText(UnityWebRequest unityRequest,
            RequestCompleteDelegate<string> onComplete,
            TextStreamHandler.TextStreamResponseDelegate onPartial = null)
        {
            // Partial text decode handler
            if (onPartial != null)
            {
                if (unityRequest.downloadHandler != null)
                {
                    VLog.E("Cannot add partial response download handler if a download handler is already set.");
                }
                else
                {
                    unityRequest.downloadHandler = new TextStreamHandler(onPartial);
                }
            }
            // Default handler
            else if (unityRequest.downloadHandler == null)
            {
                unityRequest.downloadHandler = new DownloadHandlerBuffer();
            }

            // Perform request
            return Request(unityRequest, (response, error) =>
            {
                // Request error
                string text = response?.downloadHandler?.text;
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(text, error);
                    return;
                }
                // No text returned
                if (string.IsNullOrEmpty(text))
                {
                    onComplete?.Invoke(string.Empty, "No response contents found");
                    return;
                }
                // Success
                onComplete?.Invoke(text, string.Empty);
            });
        }

        /// <summary>
        /// Performs a text request async & returns the text along with any errors
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        public async Task<RequestCompleteResponse<string>> RequestTextAsync(UnityWebRequest unityRequest,
            TextStreamHandler.TextStreamResponseDelegate onPartial = null)
        {
            // Partial text decode handler
            if (onPartial != null)
            {
                if (unityRequest.downloadHandler != null)
                {
                    VLog.E("Cannot add partial response download handler if a download handler is already set.");
                }
                else
                {
                    unityRequest.downloadHandler = new TextStreamHandler(onPartial);
                }
            }
            // Default handler
            else if (unityRequest.downloadHandler == null)
            {
                unityRequest.downloadHandler = new DownloadHandlerBuffer();
            }

            // Perform the request until completion
            return await RequestAsync(unityRequest, (request) => request?.downloadHandler?.text);
        }
        #endregion

        #region JSON
        /// <summary>
        /// Performs a request for text & decodes it into json
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJson<TData>(UnityWebRequest unityRequest,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            #pragma warning disable CS4014
            WaitRequestJsonAsync(unityRequest, onComplete, onPartial);
            #pragma warning restore CS4014
            return true;
        }

        private async Task WaitRequestJsonAsync<TData>(UnityWebRequest unityRequest,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial)
        {
            var method = unityRequest.method;
            var result = await RequestJsonAsync(unityRequest, onPartial);
            try
            {
                if (!string.IsNullOrEmpty(result.Error))
                {
                    _log.Debug($"Failed request: [{method}] {uri}. {(string.IsNullOrEmpty(payload)?string.Empty:$"Payload: {payload}.")} Error: {result.Error}");
                }
            }
            catch
            {
                _log.Debug("Failed to log request details");
            }

            onComplete?.Invoke(result.Value, result.Error);
        }

        /// <summary>
        /// Performs a request for text and decodes it into json asynchronously
        /// </summary>
        /// <param name="unityRequest">The unity request performing the post or get</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonAsync<TData>(UnityWebRequest unityRequest,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            // Set request header for json
            unityRequest.SetRequestHeader("Content-Type", "application/json");

            // Partial data if possible
            string partialJson = null;
            bool partialDecoding = false;
            RequestCompleteResponse<TData> partialResponse = new RequestCompleteResponse<TData>();

            // Set partial download handler
            TextStreamHandler.TextStreamResponseDelegate onPartialText = (jsonText) =>
            {
                // Decode async and then call partial
                partialJson = jsonText;
                partialDecoding = true;
                #pragma warning disable CS4014
                DecodePartialJsonAsync<TData>(partialJson, (response) =>
                {
                    partialResponse = response;
                    onPartial?.Invoke(partialResponse.Value, partialResponse.Error);
                    partialDecoding = false;
                });
                #pragma warning restore CS4014
            };

            // Perform text request
            var textResponse = await RequestTextAsync(unityRequest, onPartialText);
            if (!string.IsNullOrEmpty(textResponse.Error))
            {
                return new RequestCompleteResponse<TData>(default(TData), $"{textResponse.Error}. Details: {textResponse.Value ?? "null"}");
            }

            // Wait for partial decode to complete
            await TaskUtility.WaitWhile(() => partialDecoding);

            // Return previously decoded json
            if (string.Equals(partialJson, textResponse.Value))
            {
                return partialResponse;
            }

            // Decode new text
            return await DecodeJsonAsync<TData>(textResponse.Value);
        }
        // Decodes json & returns value
        private async Task<RequestCompleteResponse<TData>> DecodeJsonAsync<TData>(string jsonText)
        {
            // If string is desired result
            if (typeof(TData) == typeof(string))
            {
                object rawResult = jsonText;
                return new RequestCompleteResponse<TData>((TData)rawResult, null);
            }

            // Decode
            TData result = await JsonConvert.DeserializeObjectAsync<TData>(jsonText);
            if (result == null)
            {
                return new RequestCompleteResponse<TData>(default(TData), $"Failed to deserialize json into {typeof(TData)}\n{jsonText}");
            }

            // Return result
            return new RequestCompleteResponse<TData>(result);
        }
        // Decodes json & performs partial callback
        private async Task DecodePartialJsonAsync<TData>(string jsonText, Action<RequestCompleteResponse<TData>> onPartial)
        {
            var result = await DecodeJsonAsync<TData>(jsonText);
            onPartial?.Invoke(result);
        }

        /// <summary>
        /// Perform a json get request with a specified uri
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJsonGet<TData>(Uri uri,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            return RequestJson(new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET), onComplete, onPartial);
        }

        /// <summary>
        /// Perform a json get request with a specified uri asynchronously
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonGetAsync<TData>(Uri uri,
            RequestCompleteDelegate<TData> onPartial = null) =>
            await RequestJsonAsync<TData>(new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET), onPartial);

        /// <summary>
        /// Performs a json request by posting byte data
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postData">The data to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJsonPost<TData>(Uri uri, byte[] postData,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            var unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
            unityRequest.uploadHandler = new UploadHandlerRaw(postData);
            return RequestJson(unityRequest, onComplete, onPartial);
        }

        /// <summary>
        /// Performs a json request by posting byte data asynchronously
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postData">The data to be uploaded</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonPostAsync<TData>(Uri uri, byte[] postData,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            var unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST);
            unityRequest.uploadHandler = new UploadHandlerRaw(postData);
            return await RequestJsonAsync<TData>(unityRequest, onPartial);
        }

        /// <summary>
        /// Performs a json request by posting a string
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postText">The string to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJsonPost<TData>(Uri uri, string postText,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null) =>
            RequestJsonPost(uri, EncodeText(postText), onComplete, onPartial);

        /// <summary>
        /// Performs a json request by posting a string asynchronously
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="postText">The string to be uploaded</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonPostAsync<TData>(Uri uri, string postText,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            byte[] postData = null;
            await Task.Run(() => postData = EncodeText(postText));
            return await RequestJsonPostAsync<TData>(uri, postData, onPartial);
        }

        /// <summary>
        /// Performs a json put request with byte data
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="putData">The data to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJsonPut<TData>(Uri uri, byte[] putData,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            var unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPUT);
            unityRequest.uploadHandler = new UploadHandlerRaw(putData);
            return RequestJson(unityRequest, onComplete, onPartial);
        }

        /// <summary>
        /// Performs a json put request with byte data asynchronously
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="putData">The data to be uploaded</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonPutAsync<TData>(Uri uri, byte[] putData,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            var unityRequest = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPUT);
            unityRequest.uploadHandler = new UploadHandlerRaw(putData);
            return await RequestJsonAsync<TData>(unityRequest, onPartial);
        }

        /// <summary>
        /// Performs a json put request with text
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="putText">The text to be uploaded</param>
        /// <param name="onComplete">The delegate upon completion</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>False if the request cannot be performed</returns>
        public bool RequestJsonPut<TData>(Uri uri, string putText,
            RequestCompleteDelegate<TData> onComplete,
            RequestCompleteDelegate<TData> onPartial = null) =>
            RequestJsonPut(uri, EncodeText(putText), onComplete, onPartial);

        /// <summary>
        /// Performs a json put request with text asynchronously
        /// </summary>
        /// <param name="uri">The uri to be requested</param>
        /// <param name="putText">The text to be uploaded</param>
        /// <typeparam name="TData">The struct or class to be deserialized to</typeparam>
        /// <returns>RequestCompleteResponse with parsed data & error if applicable</returns>
        public async Task<RequestCompleteResponse<TData>> RequestJsonPutAsync<TData>(Uri uri, string putText,
            RequestCompleteDelegate<TData> onPartial = null)
        {
            byte[] putData = null;
            await Task.Run(() => putData = EncodeText(putText));
            return await RequestJsonPutAsync<TData>(uri, putData, onPartial);
        }

        // Internal helper method for encoding text
        private byte[] EncodeText(string text) => Encoding.UTF8.GetBytes(text);
        #endregion

        #region AUDIO
        /// <summary>
        /// Get audio extension from audio type
        /// </summary>
        /// <param name="audioType">The specified audio type</param>
        /// <returns>Audio extension without period.</returns>
        public static string GetAudioExtension(AudioType audioType)
        {
            switch (audioType)
            {
                // PCM
                case AudioType.UNKNOWN:
                    return "raw";
                // OGG
                case AudioType.OGGVORBIS:
                    return "ogg";
                // MP3
                case AudioType.MPEG:
                    return "mp3";
                // WAV
                case AudioType.WAV:
                    return "wav";
                default:
                    VLog.W($"Attempting to process unsupported audio type: {audioType}");
                    return audioType.ToString().ToLower();
            }
        }

        /// <summary>
        /// Get audio extension from audio type
        /// </summary>
        /// <param name="audioType">The specified audio type</param>
        /// <param name="textStream">Whether data includes text</param>
        /// <returns>Audio extension without period.</returns>
        public static string GetAudioExtension(AudioType audioType, bool textStream) =>
            GetAudioExtension(audioType) + (textStream ? "v" : "");

        /// <summary>
        /// Returns the IAudioDecoder type that works best with the specified AudioType
        /// for the current platform.
        /// </summary>
        public static Type GetAudioDecoderType(AudioType audioType)
        {
            switch (audioType)
            {
                // Assume PCM16 decoder
                case AudioType.UNKNOWN:
                    return typeof(AudioDecoderPcm);
                // MP3 decoder
                case AudioType.MPEG:
                    return typeof(AudioDecoderMp3);
            }
            // Not handled
            return null;
        }

        /// <summary>
        /// Whether or not a specific audio type can be decoded
        /// </summary>
        public static bool CanDecodeAudio(AudioType audioType) => GetAudioDecoderType(audioType) != null;

        /// <summary>
        /// Default DownloadHandlerAudioClip stream compatibility
        /// </summary>
        private static bool CanUnityStreamAudio(AudioType audioType)
        {
            // Supported via DownloadHandlerAudioClip
            if (audioType == AudioType.OGGVORBIS)
            {
                return true;
            }
            // Not supported by Unity
            return false;
        }

        /// <summary>
        /// Whether or not audio can be streamed for a specific audio type
        /// </summary>
        public static bool CanStreamAudio(AudioType audioType) =>
            CanUnityStreamAudio(audioType) || CanDecodeAudio(audioType);

        /// <summary>
        /// Instantiate an audio decoder based on the audio type to allow for
        /// complex streaming scenarios
        /// </summary>
        /// <param name="audioType">Audio decoder type allowed</param>
        /// <param name="includeJson">Whether or not json will be returned within the stream</param>
        /// <param name="onJsonDecoded">The json decode callback which will be called multiple times</param>
        public virtual IAudioDecoder GetAudioDecoder(AudioType audioType, bool includeJson = false,
            AudioJsonDecodeDelegate onJsonDecoded = null)
        {
            var decoderType = GetAudioDecoderType(audioType);
            if (decoderType == null)
            {
                return null;
            }
            var audioDecoder =  Activator.CreateInstance(decoderType) as IAudioDecoder;
            if (includeJson)
            {
                return new AudioDecoderJson(audioDecoder, onJsonDecoded);
            }
            return audioDecoder;
        }

        /// <summary>
        /// Request audio clip with uri, audio type, sample decoded delegate & completion delegate
        /// </summary>
        /// <param name="uri">The url to be called</param>
        /// <param name="audioType">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="includesJson">Whether or not json will be embedded within the audio stream.</param>
        /// <param name="onSamplesDecoded">Called one or more times as audio samples are decoded.</param>
        /// <param name="onJsonDecoded">Called one or more times as json data is decoded.</param>
        /// <param name="onComplete">Called when the audio request has completed</param>
        public bool RequestAudioStream(Uri uri, AudioType audioType, bool includesJson,
            AudioSampleDecodeDelegate onSamplesDecoded,
            AudioJsonDecodeDelegate onJsonDecoded,
            RequestCompleteDelegate<bool> onComplete) =>
            RequestAudioStream(new UnityWebRequest(uri, UnityWebRequest.kHttpVerbGET),
                audioType, includesJson, onSamplesDecoded, onJsonDecoded, onComplete);

        /// <summary>
        /// Request audio clip with web request, audio type, whether audio includes json data, callback delegates & completion delegate
        /// </summary>
        /// <param name="unityRequest">The unity request to add a download handler to</param>
        /// <param name="audioType">The audio type requested (Wav, MP3, etc.)</param>
        /// <param name="includesJson">Whether or not json will be embedded within the audio stream.</param>
        /// <param name="onSamplesDecoded">Called one or more times as audio samples are decoded.</param>
        /// <param name="onJsonDecoded">Called one or more times as json data is decoded.</param>
        /// <param name="onComplete">Called when the audio request has completed</param>
        public bool RequestAudioStream(UnityWebRequest unityRequest, AudioType audioType, bool includesJson,
            AudioSampleDecodeDelegate onSamplesDecoded,
            AudioJsonDecodeDelegate onJsonDecoded,
            RequestCompleteDelegate<bool> onComplete)
        {
            // Use custom audio stream handler
            if (unityRequest.downloadHandler == null)
            {
                // Cannot decode
                if (!CanDecodeAudio(audioType))
                {
                    var error = $"Unable to decode audio: {audioType}";
                    onComplete?.Invoke(false, error);
                    return false;
                }
                // Generate decoder
                var decoder = GetAudioDecoder(audioType, includesJson, onJsonDecoded);
                // Set audio stream handler
                unityRequest.downloadHandler = new AudioStreamHandler(decoder, onSamplesDecoded,
                    (error) => onComplete?.Invoke(string.IsNullOrEmpty(error), error));
            }

            // Perform default request operation & call stream complete once finished
            return Request(unityRequest, (response, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    onComplete?.Invoke(string.IsNullOrEmpty(error), error);
                }
            });
        }
        #endregion
    }
}
