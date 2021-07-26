/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Linq;
using System.Net;
using com.facebook.witai.data;
using com.facebook.witai.lib;
using UnityEngine;

namespace com.facebook.witai
{
    /// <summary>
    /// Manages a single request lifecycle when sending/receiving data from Wit.ai.
    ///
    /// Note: This is not intended to be instantiated directly. Requests should be created with the
    /// WitRequestFactory
    /// </summary>
    public class WitRequest
    {
        public enum Endian
        {
            Big,
            Little
        }

        /// <summary>
        /// The expected encoding of the mic pcm data
        /// </summary>
        public const string encoding = "signed-integer";
        /// <summary>
        /// The number of bits per sample
        /// </summary>
        public const int bits = 16;
        /// <summary>
        /// The sample rate used to capture audio
        /// </summary>
        public const int samplerate = 16000;
        /// <summary>
        /// The endianess of the data
        /// </summary>
        public const Endian endian = Endian.Little;

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

        const string URI_SCHEME = "https";
        const string URI_AUTHORITY = "api.wit.ai";

        const string WIT_API_VERSION = "20200513";
        private const string WIT_SDK_VERSION = "0.0.6";

        private WitConfiguration configuration;

        private Stream activeStream;

        private string command;
        private string path;

        public QueryParam[] queryParams;

        private HttpWebRequest request;
        private HttpWebResponse response;

        private Stream stream;
        private WitResponseNode responseData;

        private bool isActive;

        public byte[] postData;
        public string postContentType;

        /// <summary>
        /// Callback called when a response is received from the server
        /// </summary>
        public Action<WitRequest> onResponse;

        /// <summary>
        /// Callback called when the server is ready to receive data from the WitRequest's input
        /// stream. See WitRequest.Write()
        /// </summary>
        public Action<WitRequest> onInputStreamReady;

        /// <summary>
        /// Returns the raw string response that was received before converting it to a JSON object.
        ///
        /// NOTE: This response comes back on a different thread. Do not attempt ot set UI control
        /// values or other interactions from this callback. This is intended to be used for demo
        /// and test UI, not for regular use.
        /// </summary>
        public Action<string> onRawResponse;

        /// <summary>
        /// Returns true if a request is pending. Will return false after data has been populated
        /// from the response.
        /// </summary>
        public bool IsActive => isActive;

        /// <summary>
        /// JSON data that was received as a response from the server after onResponse has been
        /// called
        /// </summary>
        public WitResponseNode ResponseData => responseData;

        private int statusCode;
        public int StatusCode => statusCode;

        private string statusDescription;
        private bool isRequestStreamActive;
        public bool IsRequestStreamActive => IsActive && isRequestStreamActive;

        private bool isServerAuthRequired;
        public string StatusDescription => statusDescription;

        public override string ToString()
        {
            return path;
        }

        public WitRequest(WitConfiguration configuration, string path,
            params QueryParam[] queryParams)
        {
            this.configuration = configuration;
            this.command = path.Split('/').First();
            this.path = path;
            this.queryParams = queryParams;
        }

        public WitRequest(WitConfiguration configuration, string path, bool isServerAuthRequired,
            params QueryParam[] queryParams)
        {
            this.configuration = configuration;
            this.isServerAuthRequired = isServerAuthRequired;
            this.command = path.Split('/').First();
            this.path = path;
            this.queryParams = queryParams;
        }

        /// <summary>
        /// Key value pair that is sent as a query param in the Wit.ai uri
        /// </summary>
        public class QueryParam
        {
            public string key;
            public string value;
        }

        /// <summary>
        /// Start the async request for data from the Wit.ai servers
        /// </summary>
        public void Request()
        {
            UriBuilder uriBuilder = new UriBuilder();
            uriBuilder.Scheme = URI_SCHEME;
            uriBuilder.Host = URI_AUTHORITY;
            uriBuilder.Path = path;

            if (queryParams.Any())
            {
                var p = queryParams.Select(par =>
                    $"{par.key}={Uri.EscapeDataString(par.value)}");
                uriBuilder.Query = string.Join("&", p);
            }

            StartRequest(uriBuilder.Uri);
        }

        private void StartRequest(Uri uri)
        {
            if (!configuration)
            {
                statusDescription = "Configuration is not set. Cannot start request.";
                Debug.LogError(statusDescription);
                statusCode = ERROR_CODE_NO_CONFIGURATION;
                onResponse?.Invoke(this);
                return;
            }

            if (!isServerAuthRequired && string.IsNullOrEmpty(configuration.clientAccessToken))
            {
                statusDescription = "Client access token is not defined. Cannot start request.";
                Debug.LogError(statusDescription);
                statusCode = ERROR_CODE_NO_CLIENT_TOKEN;
                onResponse?.Invoke(this);
                return;
            }

            request = (HttpWebRequest) WebRequest.Create(uri);
            request.Accept = $"application/vnd.wit.{WIT_API_VERSION}+json";

            if (isServerAuthRequired)
            {
                request.Headers["Authorization"] =
                    $"Bearer {WitAuthUtility.ServerToken}";
            }
            else
            {
                request.Headers["Authorization"] =
                    $"Bearer {configuration.clientAccessToken.Trim()}";
            }

            if (null != postContentType)
            {
                request.Method = "POST";
                request.ContentType = postContentType;
                request.ContentLength = postData.Length;
            }

            // Configure additional headers
            switch (command)
            {
                case "speech":
                    request.ContentType =
                        $"audio/raw;bits={bits};rate={samplerate / 1000}k;encoding={encoding};endian={endian.ToString().ToLower()}";
                    request.Method = "POST";
                    request.SendChunked = true;
                    break;
            }

            request.UserAgent = $"wit-unity-{WIT_SDK_VERSION},{Application.productName},{SystemInfo.operatingSystem},{SystemInfo.deviceModel},{SystemInfo.deviceName}";
            Debug.Log("User agent: " + request.UserAgent);

            isActive = true;
            statusCode = 0;
            statusDescription = "Starting request";
            if (request.Method == "POST")
            {
                isRequestStreamActive = true;
                request.BeginGetRequestStream(HandleRequestStream, request);
            }

            request.BeginGetResponse(HandleResponse, request);
        }

        private void HandleResponse(IAsyncResult ar)
        {
            if (null != stream)
            {
                Debug.Log("Request stream was still open. Closing.");
                CloseRequestStream();
            }

            try
            {
                response = (HttpWebResponse) request.EndGetResponse(ar);



                statusCode = (int) response.StatusCode;
                statusDescription = response.StatusDescription;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    try
                    {
                        var responseStream = response.GetResponseStream();
                        using (var streamReader = new StreamReader(responseStream))
                        {
                            var stringResponse = streamReader.ReadToEnd();
                            onRawResponse?.Invoke(stringResponse);
                            responseData = WitResponseJson.Parse(stringResponse);
                        }

                        responseStream.Close();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                        statusCode = ERROR_CODE_GENERAL;
                        statusDescription = e.Message;
                    }
                }

                response.Close();
            }
            catch (WebException e)
            {
                statusCode = (int) e.Status;
                statusDescription = e.Message;
                Debug.LogError(e);
            }

            isActive = false;
            onResponse?.Invoke(this);
        }

        private void HandleRequestStream(IAsyncResult ar)
        {
            stream = request.EndGetRequestStream(ar);
            if (null != postData)
            {
                stream.Write(postData, 0, postData.Length);
                CloseRequestStream();
            }
            else
            {
                if (null == onInputStreamReady)
                {
                    CloseRequestStream();
                }
                else
                {
                    onInputStreamReady.Invoke(this);
                }
            }
        }

        /// <summary>
        /// Method to close the input stream of data being sent during the lifecycle of this request
        ///
        /// If a post method was used, this will need to be called before the request will complete.
        /// </summary>
        public void CloseRequestStream()
        {
            if (null != stream)
            {
                lock (stream)
                {
                    stream?.Dispose();
                    stream = null;
                }
            }

            isRequestStreamActive = false;
        }

        /// <summary>
        /// Write request data to the Wit.ai post's body input stream
        ///
        /// Note: If the stream is not open (IsActive) this will throw an IOException.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="length"></param>
        public void Write(byte[] data, int offset, int length)
        {
            if (!isRequestStreamActive)
            {
                throw new IOException(
                    "Request is not active. Call Request() on the WitRequest and wait for the onInputStreamReady callback before attempting to send data.");
            }

            if (null != stream)
            {
                lock (stream)
                {
                    stream.Write(data, offset, length);
                }
            }
        }
    }
}
