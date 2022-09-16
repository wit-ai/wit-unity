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
using Meta.WitAi.Json;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Synchronizes local enums with their Wit.Ai entities.
    /// </summary>
    internal class EnumSynchronizer
    {
        private const string GeneratedAssetsPath = @"Assets\Generated";
        private readonly IAssemblyWalker _assemblyWalker;
        private readonly IFileIo _fileIo;
        private readonly IWitHttp _witHttp;

        public EnumSynchronizer(IAssemblyWalker assemblyWalker, IFileIo fileIo, IWitHttp witHttp)
        {
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

            var witEntityNames = new List<string>();
            yield return this.GetAllWitEntityNames(list =>
            {
                if (list != null)
                {
                    witEntityNames = list;
                }
            });

            var localEnumNames = manifest.Entities.Select(entity => entity.ID).ToHashSet();

            foreach (var entityName in witEntityNames)
            {
                var onWitOnly = !localEnumNames.Contains(entityName);

                if (onWitOnly)
                {
                    yield return CreateEnumFromWitEntity(entityName);
                }
            }

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

        /// <summary>
        /// Synchronizes an enum with its corresponding Wit.Ai entity.
        /// </summary>
        /// <param name="manifestEntity">The Conduit generated entity based on the local code.</param>
        /// <param name="completionCallback">The callback to call when the sync operation is complete.</param>
        public IEnumerator Sync(ManifestEntity manifestEntity, StepResult completionCallback)
        {
            WitIncomingEntity witIncomingEntity = null;
            yield return this.GetWitEntity(manifestEntity.Name, incomingEntity => witIncomingEntity = incomingEntity);

            var delta = GetDelta(manifestEntity, witIncomingEntity);

            var result = false;
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
            WitIncomingEntity witIncomingEntity = null;
            yield return this.GetWitEntity(entityName, incomingEntity => witIncomingEntity = incomingEntity);

            if (witIncomingEntity == null)
            {
                throw new ArgumentException($"Entity {entityName} was not found on Wit.Ai");
            }

            var keywords = witIncomingEntity.Keywords.Select(keyword => keyword.Keyword).ToList();

            var wrapper = new EnumCodeWrapper(_fileIo, entityName, keywords, $"{GeneratedAssetsPath}\\{entityName}.cs");
            wrapper.WriteToFile();

        }

        private bool AddValuesToLocalEnum(ManifestEntity manifestEntity,
            EntitiesDelta delta)
        {
            if (delta.InWitOnly.Count == 0)
            {
                return true;
            }

            // TODO: Handle case when the enum does not exist at all.

            var enumWrapper = GetEnumWrapper(manifestEntity);
            if (enumWrapper == null)
            {
                return false;
            }

            var newValues = new List<string>();

            foreach (var keyword in delta.InWitOnly)
            {
                newValues.Add(keyword);
            }

            enumWrapper.AddValues(newValues);
            enumWrapper.WriteToFile();
            return true;
        }

        private EnumCodeWrapper GetEnumWrapper(ManifestEntity manifestEntity)
        {
            var qualifiedName = $"{manifestEntity.Namespace}.{manifestEntity.ID}";
            var assemblies = _assemblyWalker.GetTargetAssemblies()
                .Where(assembly => assembly.FullName == manifestEntity.Assembly).ToList();

            if (assemblies.Count() != 1)
            {
                Debug.LogError($"Expected one assembly for type {qualifiedName} but found {assemblies.Count()}");
                throw new InvalidOperationException();
            }

            var assembly = assemblies.First();

            var enumType = assembly.GetType(qualifiedName);

            _assemblyWalker.GetSourceCode(enumType, out string sourceFile);

            return new EnumCodeWrapper(_fileIo, enumType, sourceFile);
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
                delta.InWitOnly = new List<string>();

                return delta;
            }

            var witEntityValues = new HashSet<string>();

            foreach (var keyword in witEntity.Keywords)
            {
                witEntityValues.Add(keyword.Keyword);
            }

            var originalWitValues = witEntityValues.ToList();
            witEntityValues.ExceptWith(manifestEntityValues);
            manifestEntityValues.ExceptWith(originalWitValues);

            delta.InLocalOnly = manifestEntityValues.ToList();
            delta.InWitOnly = witEntityValues.ToList();

            return delta;
        }

        private IEnumerator AddValuesToWit(string entityName,
            EntitiesDelta delta, StepResult completionCallback)
        {
            var allSuccessful = true;
            foreach (var entry in delta.InLocalOnly)
            {
                var payload = "{\"keyword\": \"" + entry + "\", \"synonyms\":[]}";

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

        private IEnumerator GetAllWitEntityNames(Action<List<string>> callBack)
        {
            var response = "";
            var result = false;
            yield return _witHttp.MakeUnityWebRequest($"/entities", WebRequestMethods.Http.Get,
                (success, data) => { response = data;
                    result = success;
                });

            var entityNames = JsonConvert.DeserializeObject<List<EntityRecord>>(response);

            if (!result)
            {
                callBack(null);
                yield break;
            }

            callBack(entityNames.Select(entity => entity.name).ToList());
        }

        private IEnumerator GetWitEntity(string manifestEntityName, Action<WitIncomingEntity> callBack)
        {
            var response = "";
            var result = false;
            yield return _witHttp.MakeUnityWebRequest($"/entities/{manifestEntityName}", WebRequestMethods.Http.Get,
                (success, data) => { response = data;
                    result = success;
                });

            if (!result)
            {
                callBack(null);
                yield break;
            }

            Debug.Log($"Got entity: {response}");
            var entity = JsonConvert.DeserializeObject<WitIncomingEntity>(response);
            if (entity.Keywords == null && entity.Roles == null && entity.Name == null)
            {
                callBack(null);
            }
            else
            {
                callBack(entity);
            }
        }

        private bool EntitiesEquivalent(ManifestEntity manifestEntity, WitIncomingEntity witEntity)
        {
            var delta = GetDelta(manifestEntity, witEntity);
            return delta.IsEmpty;
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
