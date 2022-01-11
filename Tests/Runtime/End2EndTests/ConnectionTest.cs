using System;
using Facebook.WitAi.Lib;
using NUnit.Framework;
using UnityEngine;

namespace Facebook.WitAi.Tests
{
    public abstract class ConnectionTest
    {
        public string endpoint = WitRequest.WIT_ENDPOINT_SPEECH;

        public bool isSlowTest = false;

        public bool IsSlowTest => isSlowTest;
        protected string name;
        public virtual string Name
        {
            get
            {
                return name;
            }

            set
            {
                name = value;
            }
        }

        public virtual bool Success { get; protected set; } = false;
        public virtual bool TestComplete { get; protected set; } = false;
        public virtual string TestResult { get; protected set; } = "";

        public abstract string TestEndpoint { get; set; }

        public void Execute(Wit wit)
        {
            OnExecute(wit);
        }

        protected abstract void OnExecute(Wit wit);

        public abstract void OnResponse(WitResponseNode response);

        public virtual void OnError(string code, string message)
        {
            TestFailed($"Failed: {code} - {message}");
        }

        public void Reset()
        {
            Success = false;
            TestComplete = false;
            OnReset();
        }

        protected virtual void OnReset()
        {
        }

        public virtual void OnRequestCompleted()
        {
            TestComplete = true;
        }

        public virtual void OnRequestCreated(WitRequest request)
        {
            request.onRawResponse = OnRawResponse;
            request.onCustomizeUri = uriBuilder =>
            {
                Uri uri;
                if (uriBuilder.Path == endpoint)
                {
                    uri = new Uri($"http://localhost:3000/{TestEndpoint}");
                }
                else
                {
                    uri = uriBuilder.Uri;
                }

                return uri;
            };
        }

        protected virtual void OnRawResponse(string response)
        {
            Debug.Log("Raw response: " + response);
        }

        public void TestPassed()
        {
            Success = true;
        }

        public void TestFailed(string reason)
        {
            TestResult = reason;
            Success = false;
        }
    }
}
