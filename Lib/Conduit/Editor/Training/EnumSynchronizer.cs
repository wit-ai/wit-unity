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
using System.Text;
using Lib.Wit.Runtime.Requests;
using Lib.Wit.Runtime.Utilities.Logging;
using Meta.Voice.Logging;
using Meta.WitAi;
using Meta.WitAi.Data.Info;
using UnityEditor;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Synchronizes local enums with their Wit.Ai entities.
    /// </summary>
    internal class EnumSynchronizer: ILogSource
    {
        /// <inheritdoc/>
        public IVLogger Logger { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Conduit);

        private const string DEFAULT_NAMESPACE = "Conduit.Generated";

        private readonly IWitRequestConfiguration _configuration;
        private readonly IAssemblyWalker _assemblyWalker;
        private readonly IFileIo _fileIo;
        private readonly IWitVRequestFactory _requestFactory;
        private float _progress = 0;

        public EnumSynchronizer(IWitRequestConfiguration configuration, IAssemblyWalker assemblyWalker, IFileIo fileIo, IWitVRequestFactory requestFactory)
        {
            _configuration = configuration;
            _fileIo = fileIo;
            _assemblyWalker = assemblyWalker;
            _requestFactory = requestFactory;
        }

        /// <summary>
        /// Syncs all Wit.Ai entities with local enums. This method will create new code files for any missing enums.
        /// For entities that have corresponding enums, it will
        /// </summary>
        public IEnumerator SyncWitEntities(Manifest manifest, StepResult completionCallback, ConduitUtilities.ProgressDelegate progressCallback = null)
        {
            // Get all wit entity names
            // For entities not available locally, add them
            // For all other entities, sync them with manifest

            List<string> witEntityNames = null;
            _progress = 0.1f;
            progressCallback?.Invoke("Querying Wit.Ai entities", _progress);
            yield return GetEnumWitEntityNames(list =>
            {
                witEntityNames = list;
            });

            // Error handling for service failure
            if (witEntityNames == null)
            {
                completionCallback?.Invoke(false, "Failed to obtain entities from service");
                yield break;
            }

            var localEnumNames = manifest.Entities.Select(entity => entity.ID).ToList();
            const float missingProgressProgressMaxRange = 0.5f;
            var namesProgressIncrement = (missingProgressProgressMaxRange - _progress) / witEntityNames.Count;
            foreach (var entityName in witEntityNames)
            {
                progressCallback?.Invoke("Generating missing local enums", _progress);
                _progress += namesProgressIncrement;
                var onWitOnly = !localEnumNames.Contains(entityName);
                if (onWitOnly)
                {
                    yield return CreateEnumFromWitEntity(entityName);
                }
            }

            // Import newly generated entities
            AssetDatabase.Refresh();

            var allEntitiesSynced = true;
            float mergeProgressIncrement = (1f - _progress) / manifest.Entities.Count;
            foreach (var manifestEntity in manifest.Entities)
            {
                progressCallback?.Invoke($"Synchronizing entity: {manifestEntity.Name}", _progress);
                _progress += mergeProgressIncrement;
                yield return Sync(manifestEntity, (success, error) =>
                {
                    if (!success)
                    {
                        allEntitiesSynced = false;
                        Logger.Warning("Failed to sync entity {0}.\n{1}",
                            manifestEntity.Name, error);
                    }
                });
            }

            completionCallback(allEntitiesSynced, null);
        }

        private IEnumerator CreateEntityOnWit(ManifestEntity manifestEntity, StepResult completionCallback)
        {
            var entity = manifestEntity.GetAsInfo();
            var request = _requestFactory.CreateWitSyncVRequest(_configuration);
            var requestComplete = false;

            yield return ThreadUtility.CoroutineAwait(async () =>
            {
                var result = await request.RequestAddEntity(entity);
                if (string.IsNullOrEmpty(result.Error))
                {
                    completionCallback.Invoke(true, string.Empty);
                }
                else
                {
                    completionCallback.Invoke(false, $"Request to add entity {entity.name} failed on Wit.ai. Error: {result.Error}");
                }
                requestComplete = true;
            });

            yield return new WaitUntil(()=> requestComplete);
        }

        /// <summary>
        /// Synchronizes an enum with its corresponding Wit.Ai entity.
        /// </summary>
        /// <param name="manifestEntity">The Conduit generated entity based on the local code.</param>
        /// <param name="completionCallback">The callback to call when the sync operation is complete.</param>
        internal IEnumerator Sync(ManifestEntity manifestEntity, StepResult completionCallback)
        {
            WitEntityInfo? witIncomingEntity = null;
            yield return GetWitEntity(manifestEntity.Name, witEntity => witIncomingEntity = witEntity);

            var result = false;
            string witData = "";
            if (witIncomingEntity == null)
            {
                yield return CreateEntityOnWit(manifestEntity, delegate(bool success, string data)
                {
                    result = success;
                    witData = data;
                });

                if (!result)
                {
                    completionCallback(false, $"Failed to create new entity {manifestEntity.Name} on Wit.Ai.\n{witData}");
                }
                else
                {
                    completionCallback(true, "");
                }

                yield break;
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
                completionCallback(false, $"Failed to add entity {manifestEntity.Name} to local enum");
            }
        }

        private IEnumerator CreateEnumFromWitEntity(string entityName)
        {
            // Obtain wit entity
            WitEntityInfo? witIncomingEntity = null;
            yield return GetWitEntity(entityName, incomingEntity => witIncomingEntity = incomingEntity);

            // Wit entity not found
            if (!witIncomingEntity.HasValue)
            {
                Logger.Error("Enum Synchronizer - Failed to find {0} entity on Wit.AI", entityName);
                yield break;
            }

            // Get enum name & values
            var entityEnumName = ConduitUtilities.GetEntityEnumName(entityName);

            var keywords = witIncomingEntity.Value.keywords.Select(keyword => new WitKeyword(keyword)).ToList();

            // Generate wrapper
            var wrapper = new EnumCodeWrapper(_fileIo, entityEnumName, entityEnumName, keywords, DEFAULT_NAMESPACE);

            // Write to file
            wrapper.WriteToFile();
        }

        /// <summary>
        /// Adds values to local enums.
        /// </summary>
        /// <param name="manifestEntity">The entity.</param>
        /// <param name="delta">The delta between the local and remote entities.</param>
        /// <returns>True if the values were added successfully. False otherwise.</returns>
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
            var qualifiedTypeName = manifestEntity.GetQualifiedTypeName();
            var assemblies = _assemblyWalker.GetTargetAssemblies()
                .Where(assembly => assembly.FullName == manifestEntity.Assembly).ToList();

            if (assemblies.Count() != 1)
            {
                Logger.Error("Expected one assembly for type {0} but found {1}",
                    qualifiedTypeName, assemblies.Count);
                throw new InvalidOperationException();
            }

            var enumType = assemblies.First().GetType(qualifiedTypeName);

            try
            {
                return GetEnumWrapper(enumType, manifestEntity.ID);
            }
            catch (Exception)
            {
                Logger.Error("Failed to get wrapper for {0} resolved as type {1}",
                    qualifiedTypeName, enumType.FullName);
                throw;
            }
        }

        private EnumCodeWrapper GetEnumWrapper(Type enumType, string entityName)
        {
            if (!enumType.IsEnum)
            {
                return null;
            }

            _assemblyWalker.GetSourceCode(enumType, out string sourceFile, out bool singleUnit);
            if (!singleUnit)
            {
                return null;
            }

            return new EnumCodeWrapper(_fileIo, enumType, entityName, sourceFile);
        }

        /// <summary>
        /// Returns the entries that are different between Wit.Ai and Conduit.
        /// </summary>
        private EntitiesDelta GetDelta(ManifestEntity manifestEntity, WitEntityInfo? incomingEntity)
        {
            var delta = new EntitiesDelta()
            {
                Changed = new List<KeywordsDelta>(),
                WitOnly = new HashSet<WitKeyword>()
            };

            var manifestEntityKeywords = new Dictionary<string, WitKeyword>();
            foreach (var value in manifestEntity.Values)
            {
                manifestEntityKeywords.Add(value.keyword, value);
            }

            if (!incomingEntity.HasValue)
            {
                delta.LocalOnly = manifestEntity.Values.ToHashSet();
                return delta;
            }

            var witEntity = incomingEntity.Value;

            delta.LocalOnly = new HashSet<WitKeyword>();

            var witEntityKeywords = new Dictionary<string, WitKeyword>();

            foreach (var keyword in witEntity.keywords)
            {
                if (witEntityKeywords.ContainsKey(keyword.keyword))
                {
                    Logger.Warning("Duplicate keyword {0} was found in entity {1}. Verify entities on Wit.Ai",
                        keyword.keyword, incomingEntity.Value.name);
                    continue;
                }

                witEntityKeywords.Add(keyword.keyword, new WitKeyword(keyword));
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
                var request = _requestFactory.CreateWitSyncVRequest(_configuration);
                var requestError = "";
                var requestComplete = false;
                var failed = false;
                yield return ThreadUtility.CoroutineAwait(async () =>
                {
                    var result = await request.RequestAddEntityKeyword(entityName, keyword.GetAsInfo());
                    requestError = result.Error;
                    requestComplete = true;
                    if (!string.IsNullOrEmpty(requestError))
                    {
                        allSuccessful = false;
                        errorBuilder.AppendLine(
                            $"Failed to add keyword ({keyword.keyword}) to Wit.Ai. Error: {requestError}");
                        failed = true;
                    }
                });

                yield return new WaitUntil(()=> failed || requestComplete || !string.IsNullOrEmpty(requestError));
            }

            foreach (var changedKeyword in delta.Changed)
            {
                foreach (var synonym in changedKeyword.LocalOnlySynonyms)
                {
                    var request = _requestFactory.CreateWitSyncVRequest(_configuration);
                    var requestError = "";
                    var requestComplete = false;


                    yield return ThreadUtility.CoroutineAwait(async () =>
                    {
                        var result = await request.RequestAddEntitySynonym(entityName, changedKeyword.Keyword, synonym);
                        requestError = result.Error;
                        requestComplete = true;
                        if (!string.IsNullOrEmpty(requestError))
                        {
                            allSuccessful = false;
                            errorBuilder.AppendLine(
                                $"Failed to add synonym ({synonym}) to keyword ({changedKeyword.Keyword}) on Wit.Ai. Error: {requestError}");
                            requestComplete = true;
                        }
                    });

                    yield return new WaitUntil(()=> requestComplete || !string.IsNullOrEmpty(requestError));
                }
            }

            completionCallback(allSuccessful, errorBuilder.ToString());
        }

        private IEnumerator GetEnumWitEntityNames(Action<List<string>> callBack)
        {
            var request = _requestFactory.CreateWitInfoVRequest(_configuration);
            var requestCompleted = false;

            yield return ThreadUtility.CoroutineAwait(async () =>
            {
                var result = await request.RequestEntityList();

                requestCompleted = true;
                if (!string.IsNullOrEmpty(result.Error))
                {
                    Logger.Error("Failed to query Wit Entities\nError: {0}",
                        result.Error);
                    callBack(null);
                    return;
                }

                callBack(result.Value.Where(entity => !entity.name.Contains('$')).Select(entity => entity.name)
                    .ToList());
            });

            yield return new WaitUntil(() => requestCompleted);
        }

        private IEnumerator GetWitEntity(string manifestEntityName, Action<WitEntityInfo?> callBack)
        {
            var request = _requestFactory.CreateWitInfoVRequest(_configuration);
            var requestCompleted = false;

            yield return ThreadUtility.CoroutineAwait(async () =>
            {
                var result = await request.RequestEntityInfo(manifestEntityName);

                requestCompleted = true;
                if (!string.IsNullOrEmpty(result.Error))
                {
                    callBack(null);
                    return;
                }

                callBack(result.Value);
            });

            yield return new WaitUntil(() => requestCompleted);
        }
    }
}
