/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

namespace Meta.Conduit.Editor
{
    internal class WitHttp : IWitHttp
    {
        private const string BaseUri = "https://api.wit.ai";
        private const string WitApiVersion = "20220728";
        private const string WitSdkVersion = "0.0.46";
        private readonly string _serverAccessToken;

        /// <summary>
        /// The request time out in seconds.
        /// </summary>
        private int RequestTimeOut { get; set; }

        /// <summary>
        /// Initializes the class.
        /// </summary>
        /// <param name="serverAccessToken">The Wit access token.</param>
        /// <param name="requestTimeOut">The default request time out in seconds.</param>
        public WitHttp(string serverAccessToken, int requestTimeOut)
        {
            RequestTimeOut = requestTimeOut;
            _serverAccessToken = serverAccessToken;
        }

        public HttpWebRequest CreateWebRequest(string uriSection, string method, string body)
        {
            if (method != WebRequestMethods.Http.Post && method != WebRequestMethods.Http.Put)
            {
                throw new NotImplementedException("Body can only be supplied to POST and PUT requests");
            }

            var httpWebRequest = this.CreateWebRequest(uriSection, method);

            httpWebRequest.Accept = "application/json";
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.ContentLength = body.Length;

            using var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream());
            streamWriter.Write(body);

            return httpWebRequest;
        }

        public HttpWebRequest CreateWebRequest(string uriSection, string method)
        {
            var targetUrl = $"{BaseUri}{uriSection}?v={WitApiVersion}";
            HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(targetUrl);
            httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip;
            httpWebRequest.Method = method;
            httpWebRequest.UserAgent = GetUserAgent();
            httpWebRequest.Headers["Authorization"] = $"Bearer {_serverAccessToken}";
            httpWebRequest.Headers["X-Wit-Client-Request-Id"] = GetRequestId();
            httpWebRequest.Timeout = RequestTimeOut;

            return httpWebRequest;
        }

        public bool TryGetHttpResponse(HttpWebRequest httpWebRequest, out string response,  [CallerMemberName] string memberName = "")
        {
            response = null;
            try
            {
                Debug.Log($"Making {httpWebRequest.Method} request: {httpWebRequest.Address}");
                var httpResponse = (HttpWebResponse) httpWebRequest.GetResponse();
                if ((httpResponse.StatusCode != HttpStatusCode.OK) && (httpResponse.StatusCode != HttpStatusCode.Accepted))
                {
                    return false;
                }

                using var streamReader = new StreamReader(httpResponse.GetResponseStream());
                response = streamReader.ReadToEnd();
                Debug.Log(response);
                return true;
            }
            catch (WebException webException)
            {
                Debug.Log($"Failed request from {memberName}");
                Debug.LogWarning(webException);

                if (webException.Response == null)
                {
                    if (webException.Status == WebExceptionStatus.ProtocolError)
                    {
                        var statusCode = ((HttpWebResponse)webException.Response).StatusCode;
                        var statusDescription = ((HttpWebResponse)webException.Response).StatusDescription;
                        // TODO: See if we want to log those or switch on them.
                    }
                    return false;
                }
                try
                {
                    using var reader = new StreamReader(webException.Response.GetResponseStream());
                    var output = reader.ReadToEnd();
                    Debug.Log(output);
                }
                catch (Exception e)
                {
                    Debug.Log($"Failed to get error response: {e}");
                    return false;
                }

                return false;
            }
            catch (Exception e)
            {
                Debug.Log($"Failed request from {memberName}");
                Debug.LogError(e);
                return false;
            }
        }

        public UnityWebRequest CreateUnityWebRequest(string uriSection, string method)
        {
            var targetUrl = $"{BaseUri}{uriSection}?v={WitApiVersion}";
            var webRequest = new UnityWebRequest(targetUrl, method);
            webRequest.SetRequestHeader("User-Agent", GetUserAgent());
            webRequest.SetRequestHeader("Authorization", $"Bearer {_serverAccessToken}");
            webRequest.SetRequestHeader("X-Wit-Client-Request-Id", GetRequestId());
            webRequest.timeout = RequestTimeOut;
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            return webRequest;
        }

        // Get config user agent
        private static string _operatingSystem;
        private static string _deviceModel;
        private static string _appIdentifier;
        private static string _unityVersion;
        public static string GetUserAgent()
        {
            // Setup if needed
            if (_operatingSystem == null) _operatingSystem = UnityEngine.SystemInfo.operatingSystem;
            if (_deviceModel == null) _deviceModel = UnityEngine.SystemInfo.deviceModel;
            if (_appIdentifier == null) _appIdentifier = Application.identifier;
            if (_unityVersion == null) _unityVersion = Application.unityVersion;

            // Return full string
            return $"wit-unity-{WitSdkVersion},{_operatingSystem},{_deviceModel},not-yet-configured,{_appIdentifier},Editor,{_unityVersion}";
        }
        private static string GetRequestId()
        {
            return Guid.NewGuid().ToString();
        }

        private UnityWebRequest CreateUnityWebRequest(string uriSection, string method, string body)
        {
            if (method != WebRequestMethods.Http.Post && method != WebRequestMethods.Http.Put)
            {
                throw new NotImplementedException("Body can only be supplied to POST and PUT requests");
            }

            var webRequest = this.CreateUnityWebRequest(uriSection, method);

            var bytesToSend = new System.Text.UTF8Encoding().GetBytes(body);
            webRequest.uploadHandler = new UploadHandlerRaw(bytesToSend);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            return webRequest;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="uriSection"></param>
        /// <param name="method"></param>
        /// <param name="completionCallback">First parameter is success or failure and second is response text</param>
        /// <returns></returns>
        public IEnumerator MakeUnityWebRequest(string uriSection, string method, StepResult completionCallback)
        {
            yield return MakeUnityWebRequest(uriSection, method, null, completionCallback);
        }
        public IEnumerator MakeUnityWebRequest(string uriSection, string method, string body, StepResult completionCallback)
        {
            // Generate request
            UnityWebRequest webRequest;
            if (string.IsNullOrEmpty(body))
            {
                webRequest = CreateUnityWebRequest(uriSection, method);
            }
            else
            {
                webRequest = CreateUnityWebRequest(uriSection, method, body);
            }

            // Send request & wait
            webRequest.SendWebRequest();
            yield return new WaitWhile(() => !webRequest.isDone);

            // Failed
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var error = webRequest.error;
                if (string.IsNullOrEmpty(error))
                {
                    error = "Miscellaneous";
                }
                completionCallback(false, $"Failed web request. Error: {error}");
                yield break;
            }

            // Success with no response
            if (webRequest.downloadHandler == null)
            {
                completionCallback(true, "");
                yield break;
            }

            // Success with response
            string response = webRequest.downloadHandler.text;
            completionCallback(true, response);
        }
    }
}
