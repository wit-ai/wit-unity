/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.Scripting;

namespace Meta.WitAi.Json
{
    /// <summary>
    /// Class for decoding
    /// </summary>
    public static class JsonConvert
    {
        // Default converters
        public static JsonConverter[] DefaultConverters => _defaultConverters;
        private static JsonConverter[] _defaultConverters = new JsonConverter[] { new ColorConverter(), new DateTimeConverter(), new HashSetConverter<string>() };
        // Binding flags to be used for encoding/decoding
        private const BindingFlags BIND_FLAGS = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        // Ensure object exists
        private static object EnsureExists(Type objType, object obj)
        {
            if (obj == null && objType != null)
            {
                if (objType == typeof(string))
                {
                    return string.Empty;
                }
                else if (objType.IsArray)
                {
                    return Activator.CreateInstance(objType, new object[] {0});
                }
                else
                {
                    return Activator.CreateInstance(objType);
                }
            }
            return obj;
        }

        #region Deserialize
        /// <summary>
        /// Safely parse a string into a json node
        /// </summary>
        /// <param name="jsonString">Json parseable string</param>
        /// <returns>Returns json node for easy decoding</returns>
        public static WitResponseNode DeserializeToken(string jsonString)
        {
            if (string.IsNullOrEmpty(jsonString))
            {
                VLog.W($"Parse Failed\nNo content provided");
                return null;
            }

            try
            {
                return WitResponseNode.Parse(jsonString);
            }
            catch (Exception e)
            {
                VLog.W($"Parse Failed\n\n{jsonString}", e);
                return null;
            }
        }

        /// <summary>
        /// Safely parse a string into a json node async
        /// </summary>
        /// <param name="jsonString">Json parseable string</param>
        public static async Task<WitResponseNode> DeserializeTokenAsync(string jsonString)
        {
            WitResponseNode result = null;
            await Task.Run(() => result = DeserializeToken(jsonString));
            return result;
        }

        /// <summary>
        /// Generate a default instance, deserialize and return
        /// </summary>
        public static IN_TYPE DeserializeObject<IN_TYPE>(string jsonString, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE instance = (IN_TYPE)EnsureExists(typeof(IN_TYPE), null);
            return DeserializeIntoObject<IN_TYPE>(instance, jsonString, customConverters, suppressWarnings);;
        }

        /// <summary>
        /// Generate a default instance, deserialize and return async
        /// </summary>
        public static async Task<IN_TYPE> DeserializeObjectAsync<IN_TYPE>(string jsonString, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE result = default(IN_TYPE);
            await Task.Run(() => result = DeserializeObject<IN_TYPE>(jsonString, customConverters, suppressWarnings));
            return result;
        }

        /// <summary>
        /// Generate a default instance, deserialize and return
        /// </summary>
        public static IN_TYPE DeserializeObject<IN_TYPE>(WitResponseNode jsonToken, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE instance = (IN_TYPE)EnsureExists(typeof(IN_TYPE), null);
            return DeserializeIntoObject<IN_TYPE>(instance, jsonToken, customConverters, suppressWarnings);
        }

        /// <summary>
        /// Generate a default instance, deserialize and return async
        /// </summary>
        public static async Task<IN_TYPE>  DeserializeObjectAsync<IN_TYPE>(WitResponseNode jsonToken, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE result = default(IN_TYPE);
            await Task.Run(() => result = DeserializeObject<IN_TYPE>(jsonToken, customConverters, suppressWarnings));
            return result;
        }

        /// <summary>
        /// Deserialize json string into an existing instance
        /// </summary>
        public static IN_TYPE DeserializeIntoObject<IN_TYPE>(IN_TYPE instance, string jsonString, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            return DeserializeIntoObject<IN_TYPE>(instance, DeserializeToken(jsonString), customConverters, suppressWarnings);
        }

        /// <summary>
        /// Deserialize json string into an existing instance async
        /// </summary>
        public static async Task<IN_TYPE> DeserializeIntoObjectAsync<IN_TYPE>(IN_TYPE instance, string jsonString, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE result = default(IN_TYPE);
            await Task.Run(() => result = DeserializeIntoObject<IN_TYPE>(instance, jsonString, customConverters, suppressWarnings));
            return result;
        }

        /// <summary>
        /// Deserialize json string into an existing instance
        /// </summary>
        public static IN_TYPE DeserializeIntoObject<IN_TYPE>(IN_TYPE instance, WitResponseNode jsonToken, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            // Could not parse
            if (jsonToken == null)
            {
                return instance;
            }
            // Use default if no customs are added
            if (customConverters == null)
            {
                customConverters = DefaultConverters;
            }

            // Auto cast
            Type iType = typeof(IN_TYPE);
            if (iType == typeof(WitResponseNode))
            {
                object result = jsonToken;
                return (IN_TYPE)result;
            }
            if (iType == typeof(WitResponseClass))
            {
                object result = jsonToken.AsObject;
                return (IN_TYPE)result;
            }
            if (iType == typeof(WitResponseArray))
            {
                object result = jsonToken.AsArray;
                return (IN_TYPE)result;
            }

            try
            {
                StringBuilder log = new StringBuilder();
                IN_TYPE result = (IN_TYPE)DeserializeToken(iType, instance, jsonToken, log, customConverters);
                if (log.Length > 0 && !suppressWarnings)
                {
                    VLog.D($"Deserialize Warnings\n{log}");
                }
                return result;
            }
            catch (Exception e)
            {
                VLog.E($"Deserialize Failed\nTo: {typeof(IN_TYPE)}", e);
                return instance;
            }
        }

        /// <summary>
        /// Deserialize json string into an existing instance async
        /// </summary>
        public static async Task<IN_TYPE> DeserializeIntoObjectAsync<IN_TYPE>(IN_TYPE instance, WitResponseNode jsonToken, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            IN_TYPE result = default(IN_TYPE);
            await Task.Run(() => result = DeserializeIntoObject<IN_TYPE>(instance, jsonToken, customConverters, suppressWarnings));
            return result;
        }

        /// <summary>
        /// Deserialize json node into an instance of a specified type
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static object DeserializeToken(Type toType, object oldValue, WitResponseNode jsonToken, StringBuilder log, JsonConverter[] customConverters)
        {
            // Iterate custom converters
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    if (converter.CanRead && converter.CanConvert(toType))
                    {
                        return converter.ReadJson(jsonToken, toType, oldValue);
                    }
                }
            }
            // Return default
            if (toType == typeof(string))
            {
                return jsonToken.Value;
            }
            // Enum parse
            if (toType.IsEnum)
            {
                string enumStr = jsonToken.Value;
                foreach (var enumVal in Enum.GetValues(toType))
                {
                    foreach (JsonPropertyAttribute renameAttribute in toType.GetMember(enumVal.ToString())[0].GetCustomAttributes(typeof(JsonPropertyAttribute), false))
                    {
                        if (!string.IsNullOrEmpty(renameAttribute.PropertyName) && string.Equals(jsonToken.Value, renameAttribute.PropertyName, StringComparison.CurrentCultureIgnoreCase))
                        {
                            enumStr = enumVal.ToString();
                            break;
                        }
                    }
                }
                // Call try parse
                return DeserializeEnum(toType, EnsureExists(toType, oldValue), enumStr, log);
            }
            // Deserialize dictionary
            if (toType.GetInterfaces().Contains(typeof(IDictionary)))
            {
                return DeserializeDictionary(toType, EnsureExists(toType, oldValue), jsonToken.AsObject, log, customConverters);
            }
            // Deserialize List
            if (toType.GetInterfaces().Contains(typeof(IEnumerable)))
            {
                // Element type
                Type elementType = toType.GetElementType();
                if (elementType == null)
                {
                    // Try arguments
                    Type[] genericArguments = toType.GetGenericArguments();
                    if (genericArguments != null && genericArguments.Length > 0)
                    {
                        elementType = genericArguments[0];
                    }
                }

                if (elementType != null)
                {
                    // Make array
                    object newArray = newArray = typeof(JsonConvert)
                        .GetMethod("DeserializeArray", BindingFlags.Public | BindingFlags.Static)
                        .MakeGenericMethod(new Type[] { elementType })
                        .Invoke(null, new object[] { oldValue, jsonToken, log, customConverters });

                    // Return array
                    if (toType.IsArray)
                    {
                        return newArray;
                    }
                    // Convert to list
                    if (toType.GetInterfaces().Contains(typeof(IList)))
                    {
                        return Activator.CreateInstance(toType, new object[] { newArray });
                    }
                }
            }
            // Deserialize class
            if (toType.IsClass)
            {
                return DeserializeClass(toType, oldValue, jsonToken.AsObject, log, customConverters);
            }
            // Deserialize struct
            if (toType.IsValueType && !toType.IsPrimitive)
            {
                object oldStruct = Activator.CreateInstance(toType);
                object newStruct = DeserializeClass(toType, oldStruct, jsonToken.AsObject, log, customConverters);
                return newStruct;
            }

            try
            {
                // Convert to basic values
                return Convert.ChangeType(jsonToken.Value, toType);
            }
            catch (Exception e)
            {
                // Could not cast
                log.AppendLine($"\nJson Deserializer failed to cast '{jsonToken.Value}' to type '{toType}'\n{e}");
                return oldValue;
            }
        }

        // Deserialize enum
        private static MethodInfo _enumParseMethod;
        private static object DeserializeEnum(Type toType, object oldValue, string enumString, StringBuilder log)
        {
            // Find enum parse method
            if (_enumParseMethod == null)
            {
                _enumParseMethod = typeof(Enum).GetMethods().ToList().Find(method =>
                    method.IsGenericMethod && method.GetParameters().Length == 3 &&
                    string.Equals(method.Name, "TryParse"));
            }

            // Attempt to parse (Enum.TryParse<TEnum>(enumString, false, out oldValue))
            var parseMethod = _enumParseMethod.MakeGenericMethod(new[] {toType});
            object[] parseParams = new object[] {enumString, false, Activator.CreateInstance(toType)};

            // Invoke
            if ((bool)parseMethod.Invoke(null, parseParams))
            {
                // Return the parsed enum
                return parseParams[2];
            }

            // Failed
            log.AppendLine($"\nJson Deserializer Failed to cast '{enumString}' to enum type '{toType}'");
            return oldValue;
        }

        /// <summary>
        /// Deserialize a specific array
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldValue"></param>
        /// <param name="log"></param>
        /// <typeparam name="NODE_TYPE"></typeparam>
        /// <returns></returns>
        [Preserve]
        public static ITEM_TYPE[] DeserializeArray<ITEM_TYPE>(object oldArray, WitResponseNode jsonToken, StringBuilder log, JsonConverter[] customConverters)
        {
            // Failed
            if (jsonToken == null)
            {
                return (ITEM_TYPE[])oldArray;
            }

            // Generate array
            WitResponseArray jsonArray = jsonToken.AsArray;
            ITEM_TYPE[] newArray = new ITEM_TYPE[jsonArray.Count];

            // Deserialize array elements
            Type elementType = typeof(ITEM_TYPE);
            for (int i = 0; i < jsonArray.Count; i++)
            {
                object oldItem = EnsureExists(elementType, null);
                ITEM_TYPE newItem = (ITEM_TYPE) DeserializeToken(elementType, oldItem, jsonArray[i], log, customConverters);
                newArray[i] = newItem;
            }

            // Return array
            return newArray;
        }

        /// <summary>
        /// Deserialize json class into object
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static object DeserializeClass(Type toType, object oldObject, WitResponseClass jsonClass, StringBuilder log, JsonConverter[] customConverters)
        {
            // Failed
            if (jsonClass == null)
            {
                return oldObject;
            }

            // Use old value
            object newObject = oldObject;
            // Generate new if needed
            if (newObject == null)
            {
                newObject = Activator.CreateInstance(toType);
            }

            // Get all variables by token id
            Dictionary<string, IJsonVariableInfo> varDictionary = GetVarDictionary(toType, log);

            // Iterate each child node
            foreach (var childTokenName in jsonClass.ChildNodeNames)
            {
                // If not found, log & ignore
                if (!varDictionary.ContainsKey(childTokenName))
                {
                    log.AppendLine($"\t{toType.FullName} does not have a matching '{childTokenName}' field or property.");
                    continue;
                }
                // If found, ensure should deserialize
                IJsonVariableInfo varInfo = varDictionary[childTokenName];
                if (!varInfo.GetShouldDeserialize())
                {
                    log.AppendLine($"\t{toType.FullName} cannot deserialize '{childTokenName}' to the matching {(varInfo is JsonPropertyInfo ? "property" : "field")}.");
                    continue;
                }

                // Get old value
                object oldValue = varInfo.GetShouldSerialize() ? varInfo.GetValue(newObject) : null;

                // Deserialize new value
                object newValue = DeserializeToken(varInfo.GetVariableType(), oldValue, jsonClass[childTokenName], log, customConverters);

                // Apply new value
                varInfo.SetValue(newObject, newValue);
            }

            // Use deserializer if applicable
            if (toType.GetInterfaces().Contains(typeof(IJsonDeserializer)))
            {
                IJsonDeserializer deserializer = newObject as IJsonDeserializer;
                if (!deserializer.DeserializeObject(jsonClass))
                {
                    log.AppendLine($"\tIJsonDeserializer '{toType}' failed");
                }
            }

            // Success
            return newObject;
        }

        /// <summary>
        /// Deserialize a specific array
        /// </summary>
        /// <param name="node"></param>
        /// <param name="oldValue"></param>
        /// <param name="log"></param>
        /// <typeparam name="NODE_TYPE"></typeparam>
        /// <returns></returns>
        private static object DeserializeDictionary(Type toType, object oldObject, WitResponseClass jsonClass, StringBuilder log, JsonConverter[] customConverters)
        {
            // Ensure types are correct
            Type[] dictGenericTypes = toType.GetGenericArguments();
            if (dictGenericTypes == null || dictGenericTypes.Length != 2)
            {
                return oldObject;
            }

            // Generate dictionary
            IDictionary newDictionary = oldObject as IDictionary;

            // Get types
            Type keyType = dictGenericTypes[0];
            Type valType = dictGenericTypes[1];

            // Iterate children
            foreach (var childName in jsonClass.ChildNodeNames)
            {
                // Cast key if possible
                object childKey = Convert.ChangeType(childName, keyType);

                // Cast value if possible
                object newChildValue = DeserializeToken(valType, null, jsonClass[childName], log, customConverters);

                // Apply
                newDictionary[childKey] = newChildValue;
            }

            // Return dictionary
            return newDictionary;
        }
        #endregion

        #region Serialize
        /// <summary>
        /// Serializes an object into a json string
        /// </summary>
        /// <param name="inObject">The object to be serialized into json</param>
        /// <param name="customConverters">Custom json conversion interfaces</param>
        /// <param name="suppressWarnings">If true, all warnings will be ignored</param>
        /// <typeparam name="TFromType">The type of object to be decoded</typeparam>
        /// <returns>A json string corresponding to the inObject</returns>
        public static string SerializeObject<TFromType>(TFromType inObject, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            // Decode token
            WitResponseNode jsonToken = SerializeToken<TFromType>(inObject, customConverters, suppressWarnings);
            if (jsonToken != null)
            {
                try
                {
                    return jsonToken.ToString();
                }
                catch (Exception e)
                {
                    VLog.E($"Serialize Object Failed", e);
                }
            }

            // Default value
            return "{}";
        }

        /// <summary>
        /// Serializes an object into a json string asynchronously
        /// </summary>
        /// <param name="inObject">The object to be serialized into json</param>
        /// <param name="customConverters">Custom json conversion interfaces</param>
        /// <param name="suppressWarnings">If true, all warnings will be ignored</param>
        /// <typeparam name="TFromType">The type of object to be decoded</typeparam>
        /// <returns>A string after waiting for the serialization</returns>
        public static async Task<string> SerializeObjectAsync<TFromType>(TFromType inObject,
            JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            string results = null;
            await Task.Run(() => results = SerializeObject<TFromType>(inObject, customConverters, suppressWarnings));
            return results;
        }

        /// <summary>
        /// Serializes an object into a WitResponseNode
        /// </summary>
        /// <param name="inObject">The object to be serialized into json</param>
        /// <param name="customConverters">Custom json conversion interfaces</param>
        /// <param name="suppressWarnings">If true, all warnings will be ignored</param>
        /// <typeparam name="TFromType">The type of object to be decoded</typeparam>
        /// <returns>A json WitResponseNode corresponding to the inObject, or null in case of errors</returns>
        public static WitResponseNode SerializeToken<TFromType>(TFromType inObject, JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            // Return object if already serialized
            if (inObject is WitResponseNode responseNode)
            {
                return responseNode;
            }
            // Use default if no customs are added
            if (customConverters == null)
            {
                customConverters = DefaultConverters;
            }
            try
            {
                StringBuilder log = new StringBuilder();
                WitResponseNode jsonToken = SerializeToken(typeof(TFromType), inObject, log, customConverters);
                if (log.Length > 0 && !suppressWarnings)
                {
                    VLog.W($"Serialize Token Warnings\n{log}");
                }
                return jsonToken;
            }
            catch (Exception e)
            {
                VLog.E($"Serialize Token Failed for {inObject.GetType().Name}\n{inObject}", e);
            }
            return null;
        }

        /// <summary>
        /// Serializes an object into a WitResponseNode
        /// </summary>
        /// <param name="inObject">The object to be serialized into json</param>
        /// <param name="customConverters">Custom json conversion interfaces</param>
        /// <param name="suppressWarnings">If true, all warnings will be ignored</param>
        /// <typeparam name="TFromType">The type of object to be decoded</typeparam>
        /// <returns>A json WitResponseNode corresponding to the inObject, or null in case of errors</returns>
        public static async Task<WitResponseNode> SerializeTokenAsync<TFromType>(TFromType inObject,
            JsonConverter[] customConverters = null, bool suppressWarnings = false)
        {
            WitResponseNode results = null;
            await Task.Run(() => results = SerializeToken<TFromType>(inObject, customConverters, suppressWarnings));
            return results;
        }

        // Convert data to node
        private static WitResponseNode SerializeToken(Type inType, object inObject, StringBuilder log, JsonConverter[] customConverters)
        {
            // Use object type instead if possible
            if (inObject != null && inType == typeof(object))
            {
                inType = inObject.GetType();
            }
            // Already set
            if (inObject is WitResponseNode node)
            {
                return node;
            }

            // Iterate custom converters
            if (customConverters != null)
            {
                foreach (var converter in customConverters)
                {
                    if (converter.CanWrite && converter.CanConvert(inType))
                    {
                        return converter.WriteJson(inObject);
                    }
                }
            }

            // Null
            if (inObject == null)
            {
                return null;
            }
            // Most likely error in this class
            if (inType == null)
            {
                throw new ArgumentException("In Type cannot be null");
            }

            // Serialize to string
            if (inType == typeof(string))
            {
                return new WitResponseData((string)inObject);
            }
            // Convert to bool
            if (inType == typeof(bool))
            {
                return new WitResponseData((bool)inObject);
            }
            // Convert to int
            if (inType == typeof(int))
            {
                return new WitResponseData((int)inObject);
            }
            // Convert to float
            if (inType == typeof(float))
            {
                return new WitResponseData((float)inObject);
            }
            // Convert to double
            if (inType == typeof(double))
            {
                return new WitResponseData((double)inObject);
            }
            // Convert to short
            if (inType == typeof(short))
            {
                return new WitResponseData((short)inObject);
            }
            // Convert to long
            if (inType == typeof(long))
            {
                return new WitResponseData((long)inObject);
            }
            // Convert to enum
            if (inType.IsEnum)
            {
                return new WitResponseData(inObject.ToString());
            }
            // Serialize a dictionary into a node
            if (inType.GetInterfaces().Contains(typeof(IDictionary)))
            {
                IDictionary oldDictionary = (IDictionary) inObject;
                WitResponseClass newDictionary = new WitResponseClass();
                Type valType = inType.GetGenericArguments()[1];
                foreach (var key in oldDictionary.Keys)
                {
                    object newObj = oldDictionary[key];
                    if (newObj == null)
                    {
                        if (valType == typeof(string))
                        {
                            newObj = string.Empty;
                        }
                        else
                        {
                            newObj = Activator.CreateInstance(valType);
                        }
                    }
                    newDictionary.Add(key.ToString(), SerializeToken(valType, newObj, log, customConverters));
                }
                return newDictionary;
            }
            // Serialize enumerable into array
            if (inType.GetInterfaces().Contains(typeof(IEnumerable)))
            {
                // Get enum
                WitResponseArray newArray = new WitResponseArray();
                IEnumerator oldEnumerable = ((IEnumerable) inObject).GetEnumerator();

                // Array[]
                Type elementType = inType.GetElementType();

                // Try generic argument (List<>)
                if (elementType == null)
                {
                    Type[] genericArguments = inType.GetGenericArguments();
                    if (genericArguments != null && genericArguments.Length > 0)
                    {
                        elementType = genericArguments[0];
                    }
                }

                // Serialize each
                while (oldEnumerable.MoveNext())
                {
                    object newObj = EnsureExists(elementType, oldEnumerable.Current);
                    newArray.Add(string.Empty, SerializeToken(elementType, newObj, log, customConverters));
                }
                return newArray;
            }
            // Serialize a class or a struct into a node
            if (inType.IsClass || (inType.IsValueType && !inType.IsPrimitive))
            {
                return SerializeClass(inType, inObject, log, customConverters);
            }

            // Warn & incode to string
            log.AppendLine($"\tJson Serializer cannot serialize: {inType}");
            return inObject == null ? null : new WitResponseData(inObject.ToString());
        }

        // Serialize object by iterating each property & field
        private static WitResponseClass SerializeClass(Type inType, object inObject, StringBuilder log, JsonConverter[] customConverters)
        {
            // Generate result class
            WitResponseClass result = new WitResponseClass();

            // Iterate all properties & fields for the specified type
            foreach (var varInfo in GetVarInfos(inType))
            {
                // Ignore if should not serialize
                if (!varInfo.GetShouldSerialize())
                {
                    continue;
                }

                // Iterate all serialized names
                foreach (var varName in varInfo.GetSerializeNames())
                {
                    try
                    {
                        object newObj = varInfo.GetValue(inObject);
                        if (newObj != null)
                        {
                            result.Add(varName, SerializeToken(varInfo.GetVariableType(), newObj, log, customConverters));
                        }
                    }
                    catch (Exception e)
                    {
                        throw new ArgumentException($"Cannot encode '{inType.Name}.{varName}': {e.Message}", e);
                    }
                }
            }

            // Return new class
            return result;
        }
        #endregion

        #region VARIABLES
        /// <summary>
        /// Obtains all IJsonVariableInfo for a specific type's fields & properties
        /// </summary>
        private static List<IJsonVariableInfo> GetVarInfos(Type forType)
        {
            List<IJsonVariableInfo> results = new List<IJsonVariableInfo>();
            foreach (var field in forType.GetFields(BIND_FLAGS))
            {
                var info = new JsonFieldInfo(field);
                if(info.GetShouldSerialize() || info.GetShouldDeserialize()) results.Add(info);
            }

            foreach (var property in forType.GetProperties(BIND_FLAGS))
            {
                var info = new JsonPropertyInfo(property);
                if(info.GetShouldSerialize() || info.GetShouldDeserialize()) results.Add(info);
            }
            return results;
        }
        /// <summary>
        /// Obtains all variable info for a specific type with the keys being the serialized name for each
        /// </summary>
        private static Dictionary<string, IJsonVariableInfo> GetVarDictionary(Type forType, StringBuilder log)
        {
            Dictionary<string, IJsonVariableInfo> results = new Dictionary<string, IJsonVariableInfo>();
            foreach (IJsonVariableInfo info in GetVarInfos(forType))
            {
                foreach (string name in info.GetSerializeNames())
                {
                    if (string.IsNullOrEmpty(name))
                    {
                        continue;
                    }
                    if (results.ContainsKey(name))
                    {
                        log.AppendLine($"\t{forType.FullName} has two fields/properties with the same name '{name}' exposed to JsonConvert.");
                        continue;
                    }
                    results[name] = info;
                }
            }
            return results;
        }
        #endregion
    }
}
