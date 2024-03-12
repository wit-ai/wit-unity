/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Net;
using Meta.WitAi.Data.Entities;
using Meta.WitAi.Data.Intents;
using Meta.WitAi.Json;

namespace Meta.WitAi
{
    public static class WitResultUtilities
    {
        // Obsolete keys
        [Obsolete]
        public const string WIT_KEY_TRANSCRIPTION = WitConstants.KEY_RESPONSE_TRANSCRIPTION;
        [Obsolete]
        public const string WIT_KEY_INTENTS = WitConstants.KEY_RESPONSE_NLP_INTENTS;
        [Obsolete]
        public const string WIT_KEY_ENTITIES = WitConstants.KEY_RESPONSE_NLP_ENTITIES;
        [Obsolete]
        public const string WIT_KEY_TRAITS = WitConstants.KEY_RESPONSE_NLP_TRAITS;
        [Obsolete]
        public const string WIT_KEY_FINAL = WitConstants.KEY_RESPONSE_IS_FINAL;
        [Obsolete]
        public const string WIT_PARTIAL_RESPONSE = WitConstants.KEY_RESPONSE_PARTIAL;
        [Obsolete]
        public const string WIT_RESPONSE = WitConstants.KEY_RESPONSE_FINAL;
        [Obsolete]
        public const string WIT_STATUS_CODE = WitConstants.KEY_RESPONSE_CODE;
        [Obsolete]
        public const string WIT_ERROR = WitConstants.KEY_RESPONSE_ERROR;

        #region Base Response methods
        /// <summary>
        /// Returns if any status code is returned
        /// </summary>
        public static int GetStatusCode(this WitResponseNode witResponse) =>
            null != witResponse
            && witResponse.AsObject != null
            && witResponse.AsObject.HasChild(WitConstants.KEY_RESPONSE_CODE)
                ? witResponse[WitConstants.KEY_RESPONSE_CODE].AsInt
                : (int)HttpStatusCode.OK;

        /// <summary>
        /// Returns if any errors are contained in the response
        /// </summary>
        public static string GetError(this WitResponseNode witResponse) =>
            null != witResponse
            && witResponse.AsObject != null
            && witResponse.AsObject.HasChild(WitConstants.KEY_RESPONSE_ERROR)
                ? witResponse[WitConstants.KEY_RESPONSE_ERROR].Value
                : string.Empty;

        /// <summary>
        /// Get the transcription from a wit response node
        /// </summary>
        public static string GetTranscription(this WitResponseNode witResponse) =>
            null != witResponse
            && witResponse.AsObject != null
            && witResponse.AsObject.HasChild(WitConstants.KEY_RESPONSE_TRANSCRIPTION)
            ? witResponse[WitConstants.KEY_RESPONSE_TRANSCRIPTION].Value
            : string.Empty;

        /// <summary>
        /// Get whether this response is for transcriptions only
        /// </summary>
        public static WitResponseNode SafeGet(this WitResponseNode witResponse, string key)
        {
            var witObject = witResponse?.AsObject;
            return witObject != null && witObject.HasChild(key) ? witObject[key] : null;
        }

        /// <summary>
        /// Gets the content of a witResponse's partial or final response whichever is present.
        /// </summary>
        /// <param name="witResponse">The response node class or null if none was found.</param>
        /// <returns></returns>
        public static string GetResponseType(this WitResponseNode witResponse) =>
            witResponse?[WitConstants.RESPONSE_TYPE_KEY];

        /// <summary>
        /// Gets the content of a witResponse's partial or final response whichever is present.
        /// </summary>
        /// <param name="witResponse">The response node class or null if none was found.</param>
        /// <returns></returns>
        public static WitResponseClass GetResponse(this WitResponseNode witResponse) =>
            witResponse?.GetFinalResponse() ?? witResponse?.GetPartialResponse();

        /// <summary>
        /// Gets the content of a witResponse["response"] node.
        /// </summary>
        /// <param name="witResponse">The response node class or null if none was found.</param>
        public static WitResponseClass GetFinalResponse(this WitResponseNode witResponse) =>
            witResponse?.SafeGet(WitConstants.KEY_RESPONSE_FINAL)?.AsObject;

        /// <summary>
        /// Gets the content of a witResponse["partial_response"] node.
        /// </summary>
        /// <param name="witResponse">The response node class or null if none was found.</param>
        public static WitResponseClass GetPartialResponse(this WitResponseNode witResponse) =>
            witResponse?.SafeGet(WitConstants.KEY_RESPONSE_PARTIAL)?.AsObject;

        /// <summary>
        /// Get whether this response is the final response returned from the service
        /// </summary>
        public static bool GetIsFinal(this WitResponseNode witResponse) =>
            witResponse?.SafeGet(WitConstants.KEY_RESPONSE_IS_FINAL)?.AsBool ?? false;

        /// <summary>
        /// Get whether this response is a 'final' nlp response
        /// </summary>
        public static bool GetIsNlpPartial(this WitResponseNode witResponse)
        {
            var responseType = witResponse?.GetResponseType();
            return string.Equals(responseType, WitConstants.RESPONSE_TYPE_PARTIAL_NLP);
        }

        /// <summary>
        /// Get whether this response is a 'final' nlp response
        /// </summary>
        public static bool GetIsNlpFinal(this WitResponseNode witResponse)
        {
            var responseType = witResponse?.GetResponseType();
            return string.Equals(responseType, WitConstants.RESPONSE_TYPE_FINAL_NLP);
        }

        /// <summary>
        /// Get whether this response is a 'final' transcription
        /// </summary>
        public static bool GetIsTranscriptionPartial(this WitResponseNode witResponse)
        {
            var responseType = witResponse?.GetResponseType();
            return string.Equals(responseType, WitConstants.RESPONSE_TYPE_PARTIAL_TRANSCRIPTION)
                   && !string.IsNullOrEmpty(witResponse[WitConstants.KEY_RESPONSE_TRANSCRIPTION]);
        }

        /// <summary>
        /// Get whether this response is a 'final' transcription
        /// </summary>
        public static bool GetIsTranscriptionFinal(this WitResponseNode witResponse)
        {
            var responseType = witResponse?.GetResponseType();
            return string.Equals(responseType, WitConstants.RESPONSE_TYPE_FINAL_TRANSCRIPTION)
                   && !string.IsNullOrEmpty(witResponse[WitConstants.KEY_RESPONSE_TRANSCRIPTION]);
        }

        /// <summary>
        /// Get whether this response contains a transcription that should be analyzed
        /// </summary>
        public static bool GetHasTranscription(this WitResponseNode witResponse)
        {
            var responseType = witResponse?.GetResponseType();
            return (string.Equals(responseType, WitConstants.RESPONSE_TYPE_PARTIAL_TRANSCRIPTION)
                   || string.Equals(responseType, WitConstants.RESPONSE_TYPE_FINAL_TRANSCRIPTION))
                   && !string.IsNullOrEmpty(witResponse[WitConstants.KEY_RESPONSE_TRANSCRIPTION]);
        }

        // Used for multiple lookups
        private static WitResponseArray GetArray(WitResponseNode witResponse, string key) =>
            witResponse?.SafeGet(key)?.AsArray;
        #endregion

        #region Entity methods
        /// <summary>
        /// Converts wit response node into a wit entity
        /// </summary>
        public static WitEntityData AsWitEntity(this WitResponseNode witResponse) => new WitEntityData(witResponse);

        /// <summary>
        /// Converts wit response node into a float entity
        /// </summary>
        public static WitEntityFloatData AsWitFloatEntity(this WitResponseNode witResponse) => new WitEntityFloatData(witResponse);

        /// <summary>
        /// Converts wit response node into an int entity
        /// </summary>
        public static WitEntityIntData AsWitIntEntity(this WitResponseNode witResponse) => new WitEntityIntData(witResponse);

        /// <summary>
        /// Gets the string value of the first entity
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetFirstEntityValue(this WitResponseNode witResponse, string name)
        {
            return witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name]?[0]?["value"]?.Value;
        }

        /// <summary>
        /// Gets a collection of string value containing the selected value from
        /// each entity in the response.
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string[] GetAllEntityValues(this WitResponseNode witResponse, string name)
        {
            var values = new string[witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name]?.Count ?? 0];
            for (var i = 0; i < witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name]?.Count; i++)
            {
                values[i] = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name]?[i]?["value"]?.Value;
            }
            return values;
        }

        /// <summary>
        /// Gets the first entity as a WitResponseNode
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static WitResponseNode GetFirstEntity(this WitResponseNode witResponse, string name)
        {
            return witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name][0];
        }

        /// <summary>
        /// Gets the first entity with the given name as string data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static WitEntityData GetFirstWitEntity(this WitResponseNode witResponse, string name)
        {
            var array = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            return array?.Count > 0 ? array[0].AsWitEntity() : null;
        }

        /// <summary>
        /// Gets The first entity with the given name as int data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static WitEntityIntData GetFirstWitIntEntity(this WitResponseNode witResponse,
            string name)
        {
            var array = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            return array?.Count > 0 ? array[0].AsWitIntEntity() : null;
        }

        /// <summary>
        /// Gets The first entity with the given name as int data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static int GetFirstWitIntValue(this WitResponseNode witResponse,
            string name, int defaultValue)
        {
            var array = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;

            if (null == array || array.Count == 0) return defaultValue;
            return array[0].AsWitIntEntity().value;
        }

        /// <summary>
        /// Gets the first entity with the given name as float data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static WitEntityFloatData GetFirstWitFloatEntity(this WitResponseNode witResponse, string name)
        {
            var array = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            return array?.Count > 0 ? array[0].AsWitFloatEntity() : null;
        }

        /// <summary>
        /// Gets The first entity with the given name as int data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static float GetFirstWitFloatValue(this WitResponseNode witResponse,
            string name, float defaultValue)
        {
            var array = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;

            if (null == array || array.Count == 0) return defaultValue;
            return array[0].AsWitFloatEntity().value;
        }

        /// <summary>
        /// Gets all entities in the given response
        /// </summary>
        /// <param name="witResponse">The root response node of an VoiceService.events.OnResponse event</param>
        /// <returns></returns>
        public static WitEntityData[] GetEntities(this WitResponseNode witResponse, string name)
        {
            var entityJsonArray = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            var entities = new WitEntityData[entityJsonArray?.Count ?? 0];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = entityJsonArray[i].AsWitEntity();
            }

            return entities;
        }

        /// <summary>
        /// Returns the total number of entities
        /// </summary>
        /// <param name="response"></param>
        /// <returns></returns>
        public static int EntityCount(this WitResponseNode response)
        {
            return response?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?.AsArray?.Count ?? 0;
        }

        /// <summary>
        /// Gets all float entity values in the given response with the specified entity name
        /// </summary>
        /// <param name="witResponse">The root response node of an VoiceService.events.OnResponse event</param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static WitEntityFloatData[] GetFloatEntities(this WitResponseNode witResponse, string name)
        {
            var entityJsonArray = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            var entities = new WitEntityFloatData[entityJsonArray?.Count ?? 0];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = entityJsonArray[i].AsWitFloatEntity();
            }

            return entities;
        }

        /// <summary>
        /// Gets all int entity values in the given response with the specified entity name
        /// </summary>
        /// <param name="witResponse">The root response node of an VoiceService.events.OnResponse event</param>
        /// <param name="name">The entity name typically something like name:name</param>
        /// <returns></returns>
        public static WitEntityIntData[] GetIntEntities(this WitResponseNode witResponse, string name)
        {
            var entityJsonArray = witResponse?[WitConstants.KEY_RESPONSE_NLP_ENTITIES]?[name].AsArray;
            var entities = new WitEntityIntData[entityJsonArray?.Count ?? 0];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = entityJsonArray[i].AsWitIntEntity();
            }

            return entities;
        }
        #endregion

        #region Intent methods
        /// <summary>
        /// Converts wit response node into wit intent data
        /// </summary>
        public static WitIntentData AsWitIntent(this WitResponseNode witResponse) => new WitIntentData(witResponse);

        /// <summary>
        /// Gets the first intent's name
        /// </summary>
        /// <param name="witResponse"></param>
        /// <returns></returns>
        public static string GetIntentName(this WitResponseNode witResponse)
        {
            var firstIntent = witResponse.GetFirstIntent();
            return firstIntent == null ? null : firstIntent["name"]?.Value;
        }

        /// <summary>
        /// Gets the first intent node
        /// </summary>
        /// <param name="witResponse"></param>
        /// <returns></returns>
        public static WitResponseNode GetFirstIntent(this WitResponseNode witResponse)
        {
            var array = GetArray(witResponse, WitConstants.KEY_RESPONSE_NLP_INTENTS);
            return array == null || array.Count == 0 ? null : array[0];
        }

        /// <summary>
        /// Gets the first set of intent data
        /// </summary>
        /// <param name="witResponse"></param>
        /// <returns>WitIntentData or null if no intents are found</returns>
        public static WitIntentData GetFirstIntentData(this WitResponseNode witResponse)
        {
            return witResponse.GetFirstIntent()?.AsWitIntent();
        }

        /// <summary>
        /// Gets all intents in the given response
        /// </summary>
        /// <param name="witResponse">The root response node of an VoiceService.events.OnResponse event</param>
        /// <returns></returns>
        public static WitIntentData[] GetIntents(this WitResponseNode witResponse)
        {
            var array = GetArray(witResponse, WitConstants.KEY_RESPONSE_NLP_INTENTS);
            var intents = new WitIntentData[array?.Count ?? 0];
            for (int i = 0; i < intents.Length; i++)
            {
                intents[i] = array[i].AsWitIntent();
            }
            return intents;
        }
        #endregion

        #region Misc. Helper Methods
        public static string GetPathValue(this WitResponseNode response, string path)
        {

            string[] nodes = path.Trim('.').Split('.');

            var node = response;

            foreach (var nodeName in nodes)
            {
                string[] arrayElements = SplitArrays(nodeName);

                node = node[arrayElements[0]];
                for (int i = 1; i < arrayElements.Length; i++)
                {
                    node = node[int.Parse(arrayElements[i])];
                }
            }

            return node.Value;
        }
        public static void SetString(this WitResponseNode response, string path, string value)
        {

            string[] nodes = path.Trim('.').Split('.');

            var node = response;
            int nodeIndex;

            for(nodeIndex = 0; nodeIndex < nodes.Length - 1; nodeIndex++)
            {
                var nodeName = nodes[nodeIndex];
                string[] arrayElements = SplitArrays(nodeName);

                node = node[arrayElements[0]];
                for (int i = 1; i < arrayElements.Length; i++)
                {
                    node = node[int.Parse(arrayElements[i])];
                }
            }


            node[nodes[nodeIndex]] = value;
        }
        public static void RemovePath(this WitResponseNode response, string path)
        {
            string[] nodes = path.Trim('.').Split('.');

            var node = response;
            WitResponseNode parent = null;

            foreach (var nodeName in nodes)
            {
                string[] arrayElements = SplitArrays(nodeName);

                parent = node;
                node = node[arrayElements[0]];
                for (int i = 1; i < arrayElements.Length; i++)
                {
                    node = node[int.Parse(arrayElements[i])];
                }
            }

            if (null != parent) parent.Remove(node);
        }

        public static WitResponseReference GetWitResponseReference(string path)
        {

            string[] nodes = path.Trim('.').Split('.');

            var rootNode = new WitResponseReference()
            {
                path = path
            };
            var node = rootNode;

            foreach (var nodeName in nodes)
            {
                string[] arrayElements = SplitArrays(nodeName);

                var childObject = new ObjectNodeReference()
                {
                    path = path
                };
                childObject.key = arrayElements[0];
                node.child = childObject;
                node = childObject;
                for (int i = 1; i < arrayElements.Length; i++)
                {
                    var childIndex = new ArrayNodeReference()
                    {
                        path = path
                    };
                    childIndex.index = int.Parse(arrayElements[i]);
                    node.child = childIndex;
                    node = childIndex;
                }
            }

            return rootNode;
        }

        public static string GetCodeFromPath(string path)
        {
            string[] nodes = path.Trim('.').Split('.');
            string code = "witResponse";
            foreach (var nodeName in nodes)
            {
                string[] arrayElements = SplitArrays(nodeName);

                code += $"[\"{arrayElements[0]}\"]";
                for (int i = 1; i < arrayElements.Length; i++)
                {
                    code += $"[{arrayElements[i]}]";
                }
            }

            code += ".Value";
            return code;
        }

        private static string[] SplitArrays(string nodeName)
        {
            var nodes = nodeName.Split('[');
            for (int i = 0; i < nodes.Length; i++)
            {
                nodes[i] = nodes[i].Trim(']');
            }

            return nodes;
        }
        #endregion

        #region Trait Methods
        /// <summary>
        /// Gets the string value of the first trait
        /// </summary>
        /// <param name="witResponse"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetTraitValue(this WitResponseNode witResponse, string name)
        {
            return witResponse?[WitConstants.KEY_RESPONSE_NLP_TRAITS]?[name]?[0]?["value"]?.Value;
        }
        #endregion
    }

    #region WitResponseReference Child Classes
    public class WitResponseReference
    {
        public WitResponseReference child;
        public string path;

        public virtual string GetStringValue(WitResponseNode response)
        {
            return child.GetStringValue(response);
        }

        public virtual int GetIntValue(WitResponseNode response)
        {
            return child.GetIntValue(response);
        }

        public virtual float GetFloatValue(WitResponseNode response)
        {
            return child.GetFloatValue(response);
        }
    }

    public class ArrayNodeReference : WitResponseReference
    {
        public int index;

        public override string GetStringValue(WitResponseNode response)
        {
            if (null != child)
            {
                return child.GetStringValue(response[index]);
            }

            return response[index].Value;
        }

        public override int GetIntValue(WitResponseNode response)
        {
            if (null != child)
            {
                return child.GetIntValue(response[index]);
            }

            return response[index].AsInt;
        }

        public override float GetFloatValue(WitResponseNode response)
        {
            if (null != child)
            {
                return child.GetFloatValue(response[index]);
            }

            return response[index].AsInt;
        }
    }

    public class ObjectNodeReference : WitResponseReference
    {
        public string key;

        public override string GetStringValue(WitResponseNode response)
        {
            if (null != child && null != response?[key])
            {
                return child.GetStringValue(response[key]);
            }

            return response?[key]?.Value;
        }

        public override int GetIntValue(WitResponseNode response)
        {
            if (null != child)
            {
                return child.GetIntValue(response[key]);
            }

            return response[key].AsInt;
        }

        public override float GetFloatValue(WitResponseNode response)
        {
            if (null != child)
            {
                return child.GetFloatValue(response[key]);
            }

            return response[key].AsFloat;
        }
    }
    #endregion
}
