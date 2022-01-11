using Facebook.WitAi;
using Facebook.WitAi.Lib;
using UnityEngine;

namespace Facebook.WitAi.Tests
{
    public class TestErrorCode : ConnectionTest
    {
         public int code;
         private string testName = "default";
         private string testEndpoint = "default";
         public bool anyErrorCode = false;

        public override string Name
        {
            get
            {
                return string.IsNullOrEmpty(testName) || testName == "default" ? "Test Error " + code : testName;
            }
            set
            {
                testName = value;
            }
        }

        public override string TestEndpoint
        {
            get
            {
                return string.IsNullOrEmpty(testEndpoint) || testEndpoint == "default" ? "error" + code : testEndpoint;
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
            TestFailed("Should have received error " + code);
        }

        public override void OnError(string code, string message)
        {
            if (code == "HTTP Error " + this.code || anyErrorCode)
            {
                TestPassed();
            }
            else
            {
                TestFailed($"Received {code}: {message}");
            }
        }
    }
}
