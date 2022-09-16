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
        private const string VersionSuffix = "?v=20210928";
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
            var targetUrl = $"{BaseUri}{uriSection}{VersionSuffix}";
            HttpWebRequest httpWebRequest = (HttpWebRequest) WebRequest.Create(targetUrl);
            httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip;
            httpWebRequest.Method = method;
            httpWebRequest.Headers["Authorization"] = $"Bearer {_serverAccessToken}";
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
            var targetUrl = $"{BaseUri}{uriSection}{VersionSuffix}";
            var webRequest = new UnityWebRequest(targetUrl, method);
            webRequest.SetRequestHeader("Authorization", $"Bearer {_serverAccessToken}");
            webRequest.timeout = RequestTimeOut;
            webRequest.downloadHandler = new DownloadHandlerBuffer();

            return webRequest;
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
            using var webRequest = CreateUnityWebRequest(uriSection, method);
            Debug.Log($"Making {webRequest.method} request: {webRequest.uri}");

            webRequest.SendWebRequest();
            yield return new WaitWhile(() => !webRequest.isDone);

            var response = webRequest.downloadHandler?.text;

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                var error = String.IsNullOrEmpty(response) ? webRequest.error : response;
                completionCallback(false, $"Failed web request. Error: {error}");
                yield break;
            }

            if (webRequest.downloadHandler == null)
            {
                completionCallback(true, "");
                yield break;
            }
            Debug.Log(response);
            completionCallback(true, response);
        }

        // TODO: This should be merged into the method above.
        public IEnumerator MakeUnityWebRequest(string uriSection, string method, string body, StepResult completionCallback)
        {
            using var webRequest = CreateUnityWebRequest(uriSection, method, body);

            webRequest.SendWebRequest();
            yield return new WaitWhile(() => !webRequest.isDone);

            var response = webRequest.downloadHandler?.text;

            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                completionCallback(false, String.IsNullOrEmpty(response)?webRequest.error: response);
                yield break;
            }

            Debug.Log(response);
            completionCallback(true, response);
        }
    }
}
