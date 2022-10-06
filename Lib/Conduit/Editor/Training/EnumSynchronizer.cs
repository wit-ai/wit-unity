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
using Meta.WitAi;
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
            yield return this.GetWitEntity(manifestEntity.Name, incomingEntity => witIncomingEntity = incomingEntity);

            var result = false;
            if (witIncomingEntity == null)
            {
                yield return this.CreateEntityOnWit(manifestEntity.Name, delegate(bool success, string data)
                    { result = success; });

                if (!result)
                {
                    completionCallback(false, $"Failed to create new entity {manifestEntity.Name} on Wit.Ai");
                    yield break;
                }
            }

            var delta = GetDelta(manifestEntity, witIncomingEntity);

            result = false;
            yield return AddValuesToWit(manifestEntity.Name, delta,
                delegate(bool success, string data) { result = success; });

            if (!result)
            {
                completionCallback(false, "Failed to add values to Wit.Ai");
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
            if (delta.InWitOnly.Count == 0)
            {
                return true;
            }

            var enumWrapper = GetEnumWrapper(manifestEntity);
            if (enumWrapper == null)
            {
                return false;
            }

            var newValues = new List<WitKeyword>();

            foreach (var keyword in delta.InWitOnly)
            {
                newValues.Add(new WitKeyword()
                {
                    keyword = keyword.keyword
                });
            }

            enumWrapper.AddValues(newValues);
            enumWrapper.WriteToFile();
            return true;
        }

        private EnumCodeWrapper GetEnumWrapper(ManifestEntity manifestEntity)
        {
            var qualifiedName = string.IsNullOrEmpty(manifestEntity.Namespace)
                ? $"{manifestEntity.ID}"
                : $"{manifestEntity.Namespace}.{manifestEntity.ID}";
            var assemblies = _assemblyWalker.GetTargetAssemblies()
                .Where(assembly => assembly.FullName == manifestEntity.Assembly).ToList();

            if (assemblies.Count() != 1)
            {
                Debug.LogError($"Expected one assembly for type {qualifiedName} but found {assemblies.Count()}");
                throw new InvalidOperationException();
            }

            var enumType = assemblies.First().GetType(qualifiedName);

            return GetEnumWrapper(enumType, manifestEntity.ID);
        }

        private EnumCodeWrapper GetEnumWrapper(Type enumType, string entityName)
        {
            _assemblyWalker.GetSourceCode(enumType, out string sourceFile);

            return new EnumCodeWrapper(_fileIo, enumType, entityName, sourceFile);
        }

        /// <summary>
        /// Returns the entries that are in one or the other.
        /// </summary>
        private EntitiesDelta GetDelta(ManifestEntity manifestEntity, WitIncomingEntity witEntity)
        {
            var delta = new EntitiesDelta();

            var manifestEntityValues = new HashSet<string>();
            foreach (var value in manifestEntity.Values)
            {
                manifestEntityValues.Add(value);
            }

            if (witEntity == null)
            {
                delta.InLocalOnly = manifestEntityValues.ToList();
                delta.InWitOnly = new List<Meta.WitAi.Data.Info.WitEntityKeywordInfo>();

                return delta;
            }

            var witEntityValues = new HashSet<string>();

            foreach (var keyword in witEntity.keywords)
            {
                witEntityValues.Add(keyword.keyword);
            }

            var originalWitValues = witEntityValues.ToList();
            witEntityValues.ExceptWith(manifestEntityValues);
            manifestEntityValues.ExceptWith(originalWitValues);

            delta.InLocalOnly = manifestEntityValues.ToList();
            delta.InWitOnly = witEntityValues.ToList().Select(keyword => new Meta.WitAi.Data.Info.WitEntityKeywordInfo
            {
                keyword = keyword
            }).ToList();

            return delta;
        }

        private IEnumerator AddValuesToWit(string entityName,
            EntitiesDelta delta, StepResult completionCallback)
        {
            var allSuccessful = true;
            foreach (var entry in delta.InLocalOnly)
            {
                var keyword = new WitKeyword()
                {
                    keyword = entry,
                    synonyms = new List<string>()
                };
                var payload = JsonConvert.SerializeObject(keyword);

                yield return _witHttp.MakeUnityWebRequest($"/entities/{entityName}/keywords",
                    WebRequestMethods.Http.Post, payload, delegate(bool success, string data)
                    {
                        if (!success)
                        {
                            allSuccessful = false;
                        }
                    });
            }

            completionCallback(allSuccessful, "");
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

            var entityNames = JsonConvert.DeserializeObject<List<EntityRecord>>(response);
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

        /// <summary>
        /// Private class used for deserializing entity records from Wit.Ai.
        /// </summary>
        private struct EntityRecord
        {
            public string name;
        }
    }
}
