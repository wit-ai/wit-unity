using System.Collections;
using System.Collections.Generic;
using System.Net;
using Facebook.WitAi;
using Facebook.WitAi.Configuration;
using Facebook.WitAi.Data.Configuration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Moq;

namespace Tests
{
    // In this test suit, the class under test is "WitRequest" in "WitRequest.cs" file.
    public class WitRequest_Tests
    {
        [Test]
        public void WitRequset_Test()
        {
            var mock = new Mock<WebRequest>();
            
            /// Send text data to Wit.ai for NLU processing
            string transcription = "Make the cube red.";
            WitRequestOptions requestOptions = new WitRequestOptions();
            // TODO: Load the asset ScriptableObject named "WitConfiguration.asset"
            var runtimeConfiguration = new WitRuntimeConfiguration();
            
            runtimeConfiguration.witConfiguration = FindDefaultWitConfig();
            WitRequest activeRequest = runtimeConfiguration.witConfiguration.MessageRequest(transcription, requestOptions);
            activeRequest.onResponse = HandleResult;
            activeRequest.Request();
        }

        /// <summary>
        /// Handles results back from wit.ai
        /// </summary>
        /// <param name="request"></param>
        private void HandleResult(WitRequest request)
        {
            if (request.StatusCode == (int) HttpStatusCode.OK)
            {
                if (null != request.ResponseData)
                {
                    //events?.OnResponse?.Invoke(request.ResponseData);
                    // TODO: check whether the message (e.g. Make the cube red.) has been processed correctly.
                }
                else
                {
                    Assert.Fail("No data was returned from the server.");
                    //events?.OnError?.Invoke("No Data", "No data was returned from the server.");
                }
            }
            else
            {                
                if (request.StatusCode != WitRequest.ERROR_CODE_ABORTED)
                {
                    Assert.Fail("HTTP Error " + request.StatusCode);
                    //events?.OnError?.Invoke("HTTP Error " + request.StatusCode,
                    //    request.StatusDescription);
                }
                else // Request was aborted.
                {
                    Assert.Fail("Request was aborted.");
                    //events?.OnAborted?.Invoke();
                }
            }

            Assert.Pass();
        }

        public static WitConfiguration FindDefaultWitConfig()
        {
            string[] guids = AssetDatabase.FindAssets("name:WitConfiguration"); // filename: WitConfiguration.asset
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]); // Just send the first one because we don't have two wit config file with the same name (hopefully).
                return AssetDatabase.LoadAssetAtPath<WitConfiguration>(path);
            }

            return null;
        }

    }

}
