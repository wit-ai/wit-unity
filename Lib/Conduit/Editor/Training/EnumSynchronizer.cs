/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using Meta.WitAi;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Json;
using UnityEditor;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Synchronizes local enums with their Wit.Ai entities.
    /// </summary>
    internal class EnumSynchronizer
    {
        private const string DEFAULT_NAMESPACE = "Conduit.Generated";

        private readonly IWitRequestConfiguration _configuration;
        private readonly IAssemblyWalker _assemblyWalker;
        private readonly IFileIo _fileIo;
        private readonly IWitHttp _witHttp;

        public EnumSynchronizer(IWitRequestConfiguration configuration, IAssemblyWalker assemblyWalker, IFileIo fileIo, IWitHttp witHttp)
        {
            _configuration = configuration;
            _fileIo = fileIo;
            _assemblyWalker = assemblyWalker;
            _witHttp = witHttp;
        }

        /// <summary>
        /// Syncs all Wit.Ai entities with local enums. This method will create new code files for any missing enums.
        /// For entities that have corresponding enums, it will
        /// </summary>
        public IEnumerator SyncWitEntities(Manifest manifest, StepResult completionCallback)
        {
            // Get all wit entity names
            // For entities not available locally, add them
            // For all other entities, sync them with manifest

            List<string> witEntityNames = null;
            yield return this.GetEnumWitEntityNames(list =>
            {
                witEntityNames = list;
            });

            // Error handling for service failure
            if (witEntityNames == null)
            {
                completionCallback?.Invoke(false, "Failed to obtain entities from service");
                yield break;
            }

            // Use list
            var localEnumNames = manifest.Entities.Select(entity => entity.ID).ToList();
            foreach (var entityName in witEntityNames)
            {
                var onWitOnly = !localEnumNames.Contains(entityName);

                if (onWitOnly)
                {
                    yield return CreateEnumFromWitEntity(entityName);
                }
            }

            // Import newly generated entities
            AssetDatabase.Refresh();

            bool allEntitiesSynced = true;
            foreach (var manifestEntity in manifest.Entities)
            {
                yield return Sync(manifestEntity, (success, data) =>
                {
                    if (!success)
                    {
                        allEntitiesSynced = false;
                        VLog.W($"Failed to sync entity {manifestEntity.Name}.\n{data}");
                    }
                });
            }

            completionCallback(allEntitiesSynced, null);
        }

        private IEnumerator CreateEntityOnWit(string entityName, StepResult completionCallback)
        {
            var entity = new WitIncomingEntity()
            {
                name = entityName
            };

            var outgoingEntity = new WitOutgoingEntity(entity);
            var payload = JsonConvert.SerializeObject(outgoingEntity);

            yield return _witHttp.MakeUnityWebRequest($"/entities",
                WebRequestMethods.Http.Post, payload, completionCallback);
        }

        /// <summary>
        /// Synchronizes an enum with its corresponding Wit.Ai entity.
        /// </summary>
        /// <param name="manifestEntity">The Conduit generated entity based on the local code.</param>
        /// <param name="completionCallback">The callback to call when the sync operation is complete.</param>
        internal IEnumerator Sync(ManifestEntity manifestEntity, StepResult completionCallback)
        {
            WitIncomingEntity witIncomingEntity = null;
            yield return GetWitEntity(manifestEntity.Name, incomingEntity => witIncomingEntity = incomingEntity);

            var result = false;
            string witData = "";
            if (witIncomingEntity == null)
            {
                yield return CreateEntityOnWit(manifestEntity.Name, delegate(bool success, string data)
                {
                    result = success;
                    witData = data;
                });

                if (!result)
                {
                    completionCallback(false, $"Failed to create new entity {manifestEntity.Name} on Wit.Ai.\n{witData}");
                    yield break;
                }
            }

            var delta = GetDelta(manifestEntity, witIncomingEntity);

            result = false;
            yield return AddValuesToWit(manifestEntity.Name, delta,
                delegate(bool success, string data)
                {
                    result = success; 
                    witData = data;
                });
            
            if (!result)
            {
                completionCallback(false, $"Failed to add values to Wit.Ai\n{witData}");
                yield break;
            }

            if (AddValuesToLocalEnum(manifestEntity, delta))
            {
                completionCallback(true, "");
            }
            else
            {
                completionCallback(false, "Failed to add entity to local enum");
            }
        }

        private IEnumerator CreateEnumFromWitEntity(string entityName)
        {
            // Obtain wit entity
            WitIncomingEntity witIncomingEntity = null;
            yield return this.GetWitEntity(entityName, incomingEntity => witIncomingEntity = incomingEntity);

            // Wit entity not found
            if (witIncomingEntity == null)
            {
                Debug.LogError($"Enum Synchronizer - Failed to find {entityName} entity on Wit.AI");
                yield break;
            }

            // Get enum name & values
            var entityEnumName = ConduitUtilities.GetEntityEnumName(entityName);

            // Generate wrapper
            var wrapper = new EnumCodeWrapper(_fileIo, entityEnumName, entityEnumName, witIncomingEntity.keywords, DEFAULT_NAMESPACE);

            // Write to file
            wrapper.WriteToFile();
        }

        private bool AddValuesToLocalEnum(ManifestEntity manifestEntity,
            EntitiesDelta delta)
        {
            if (delta.WitOnly.Count == 0 && delta.Changed.Count == 0)
            {
                return true;
            }

            var enumWrapper = GetEnumWrapper(manifestEntity);
            if (enumWrapper == null)
            {
                return false;
            }

            var newValues = new List<WitKeyword>();

            foreach (var keyword in delta.WitOnly)
            {
                newValues.Add(keyword);
            }
            
            foreach (var changedValue in delta.Changed)
            {
                var keyword = new WitKeyword(changedValue.Keyword, changedValue.AllSynonyms.ToList());
                newValues.Add(keyword);
            }
            
            enumWrapper.AddValues(newValues);
            enumWrapper.WriteToFile();
            return true;
        }

        private EnumCodeWrapper GetEnumWrapper(ManifestEntity manifestEntity)
        {
            var qualifiedTypeName = string.IsNullOrEmpty(manifestEntity.Namespace)
                ? $"{manifestEntity.ID}"
                : $"{manifestEntity.Namespace}.{manifestEntity.ID}";
            var assemblies = _assemblyWalker.GetTargetAssemblies()
                .Where(assembly => assembly.FullName == manifestEntity.Assembly).ToList();

            if (assemblies.Count() != 1)
            {
                Debug.LogError($"Expected one assembly for type {qualifiedTypeName} but found {assemblies.Count()}");
                throw new InvalidOperationException();
            }

            var enumType = assemblies.First().GetType(qualifiedTypeName);

            try
            {
                return GetEnumWrapper(enumType, manifestEntity.ID);
            }
            catch (Exception)
            {
                VLog.E($"Failed to get wrapper for {qualifiedTypeName} resolved as type {enumType.FullName}");
                throw;
            }
        }

        private EnumCodeWrapper GetEnumWrapper(Type enumType, string entityName)
        {
            _assemblyWalker.GetSourceCode(enumType, out string sourceFile);

            return new EnumCodeWrapper(_fileIo, enumType, entityName, sourceFile);
        }

        /// <summary>
        /// Returns the entries that are different between Wit.Ai and Conduit.
        /// </summary>
        private EntitiesDelta GetDelta(ManifestEntity manifestEntity, WitIncomingEntity witEntity)
        {
            var delta = new EntitiesDelta();

            var manifestEntityKeywords = new Dictionary<string, WitKeyword>();
            foreach (var value in manifestEntity.Values)
            {
                manifestEntityKeywords.Add(value.keyword, value);
            }

            if (witEntity == null)
            {
                delta.LocalOnly = manifestEntity.Values.ToHashSet();
                delta.WitOnly = new HashSet<WitKeyword>();
                return delta;
            }

            delta.LocalOnly = new HashSet<WitKeyword>();
            delta.WitOnly = new HashSet<WitKeyword>();
            
            var witEntityKeywords = new Dictionary<string, WitKeyword>();

            foreach (var keyword in witEntity.keywords)
            {
                witEntityKeywords.Add(keyword.keyword, keyword);
            }

            var commonKeywords = new HashSet<string>();

            foreach (var witEntityKeyword in witEntityKeywords)
            {
                if (manifestEntityKeywords.ContainsKey(witEntityKeyword.Key))
                {
                    commonKeywords.Add(witEntityKeyword.Key);
                }
                else
                {
                    delta.WitOnly.Add(witEntityKeyword.Value);
                }
            }

            foreach (var manifestEntityKeyword in manifestEntityKeywords)
            {
                if (!witEntityKeywords.ContainsKey(manifestEntityKeyword.Key))
                {
                    delta.LocalOnly.Add(manifestEntityKeyword.Value);
                }
            }

            delta.Changed = new List<KeywordsDelta>();
            foreach (var commonKeyword in commonKeywords)
            {
                var synonymsDelta = GetKeywordsDelta(manifestEntityKeywords[commonKeyword],
                    witEntityKeywords[commonKeyword]);
                
                if(!synonymsDelta.IsEmpty)
                {
                    delta.Changed.Add(synonymsDelta);
                }
            }
           
            return delta;
        }

        private KeywordsDelta GetKeywordsDelta(WitKeyword localEntityKeyword, WitKeyword witEntityKeyword)
        {
            if (localEntityKeyword.keyword != witEntityKeyword.keyword)
            {
                throw new InvalidOperationException("Mismatching keywords when checking for synonyms delta");
            }
            
            var delta = new KeywordsDelta()
            {
                Keyword = localEntityKeyword.keyword,
                LocalOnlySynonyms = new HashSet<string>(),
                WitOnlySynonyms = new HashSet<string>(),
                AllSynonyms = new HashSet<string>()
            };
            
            foreach (var witSynonym in witEntityKeyword.synonyms)
            {
                delta.AllSynonyms.Add(witSynonym);
                if (!localEntityKeyword.synonyms.Contains(witSynonym) && !localEntityKeyword.keyword.Equals(witSynonym))
                {
                    delta.WitOnlySynonyms.Add(witSynonym);
                }
            }

            foreach (var localSynonym in localEntityKeyword.synonyms)
            {
                delta.AllSynonyms.Add(localSynonym);
                if (!witEntityKeyword.synonyms.Contains(localSynonym) && !witEntityKeyword.keyword.Equals(localSynonym))
                {
                    delta.LocalOnlySynonyms.Add(localSynonym);
                }
            }

            return delta;
        }

        private IEnumerator AddValuesToWit(string entityName,
            EntitiesDelta delta, StepResult completionCallback)
        {
            var errorBuilder = new StringBuilder();
            var allSuccessful = true;
            foreach (var keyword in delta.LocalOnly)
            {
                var payload = JsonConvert.SerializeObject(keyword);

                yield return _witHttp.MakeUnityWebRequest($"/entities/{entityName}/keywords",
                    WebRequestMethods.Http.Post, payload, delegate(bool success, string data)
                    {
                        if (!success)
                        {
                            allSuccessful = false;
                            errorBuilder.AppendLine($"Failed to add keyword ({keyword.keyword}) to Wit.Ai");
                        }
                    });
            }

            foreach (var changedKeyword in delta.Changed)
            {
                foreach (var synonym in changedKeyword.LocalOnlySynonyms)
                {
                    var payload = $"{{\"synonym\": \"{synonym}\"}}";

                    yield return _witHttp.MakeUnityWebRequest($"/entities/{entityName}/keywords/{changedKeyword.Keyword}/synonyms",
                        WebRequestMethods.Http.Post, payload, delegate(bool success, string data)
                        {
                            if (!success)
                            {
                                allSuccessful = false;
                                errorBuilder.AppendLine($"Failed to add synonym ({synonym}) to Wit.Ai");
                            }
                        });
                }
            }

            completionCallback(allSuccessful, errorBuilder.ToString());
        }

        private IEnumerator GetEnumWitEntityNames(Action<List<string>> callBack)
        {
            var response = "";
            var result = false;
            yield return _witHttp.MakeUnityWebRequest($"/entities", WebRequestMethods.Http.Get,
                (success, data) =>
                {
                    response = data;
                    result = success;
                });

            if (!result)
            {
                Debug.LogError($"Wit Entities Load Failed\nError: {response}");
                callBack(null);
                yield break;
            }

            var entityNames = JsonConvert.DeserializeObject<List<WitEntityInfo>>(response);
            if (entityNames == null)
            {
                Debug.LogError($"Wit Entities Decode Failed\nJSON:\n{response}");
                callBack(null);
                yield break;
            }

            callBack(entityNames.Where(entity => !entity.name.Contains('$')).Select(entity => entity.name).ToList());
        }

        private IEnumerator GetWitEntity(string manifestEntityName, Action<WitIncomingEntity> callBack)
        {
            var response = "";
            var result = false;
            yield return _witHttp.MakeUnityWebRequest($"/entities/{manifestEntityName}", WebRequestMethods.Http.Get,
                (success, data) =>
                {
                    response = data;
                    result = success;
                });

            if (!result)
            {
                VLog.D($"Wit {manifestEntityName} Entity not found on Wit\nError: {response}");
                callBack(null);
                yield break;
            }

            var entity = JsonConvert.DeserializeObject<WitIncomingEntity>(response);
            if (entity.keywords == null && entity.roles == null && entity.name == null)
            {
                VLog.E($"Wit {manifestEntityName} Entity Decode Failed\nKeywords: {(entity.keywords == null)}\nRoles: {(entity.roles == null)}\nName: {(entity.name == null)}\nJSON:\n{response}");
                callBack(null);
                yield break;
            }

            callBack(entity);
        }
    }
}
