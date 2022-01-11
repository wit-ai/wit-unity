using System;
using Facebook.WitAi.CallbackHandlers;
using Facebook.WitAi.Lib;
using NUnit.Framework;
using UnityEngine;

namespace Facebook.WitAi.Tests
{
    public class Message1024BoundaryTest : ConnectionTest
    {
        [SerializeField] public string testEndpoint;
        [SerializeField] public string testName;
        [SerializeField] public ExpectedResult[] expectedResults;
        public override string Name => testName;
        public override string TestEndpoint
        {
            get
            {
                return testEndpoint;
            }
            set
            {
                testEndpoint = value;
            }
        }

        protected override void OnExecute(Wit wit)
        {
            wit.ActivateImmediately();
        }

        public override void OnResponse(WitResponseNode response)
        {
            for (int i = 0; i < expectedResults.Length; i++)
            {
                var pathRef = WitResultUtilities.GetWitResponseReference(expectedResults[i].path);
                var value = pathRef.GetStringValue(response);
                var expected = expectedResults[i].value;
                
                if (value != expected)
                {
                    TestFailed($"Expected: {expected}, got: {value}");
                    return;
                }
            }

            TestPassed();
        }
    }

    [Serializable]
    public class ExpectedResult
    {
        public string path;
        public string value;
    }
}
