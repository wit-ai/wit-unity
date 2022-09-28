/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Meta.WitAi;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Lib.Editor;
using UnityEditor;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Synchronizes local enums with their Wit.Ai entities.
    /// </summary>
    internal class EnumSynchronizer
    {
        private readonly IWitRequestConfiguration _config;
        private readonly IAssemblyWalker _assemblyWalker;
        private readonly IFileIo _fileIo;

        public EnumSynchronizer(IWitRequestConfiguration configuration, IAssemblyWalker assemblyWalker, IFileIo fileIo)
        {
            _config = configuration;
            _fileIo = fileIo;
            _assemblyWalker = assemblyWalker;
        }

        /// <summary>
        /// Syncs all Wit.Ai entities with local enums. This method will create new code files for any missing enums.
        /// For entities that have corresponding enums, it will
        /// </summary>
        public IEnumerator SyncWitEntities(Manifest manifest, StepResult completionCallback)
        {
            // Invalid app info
            if (_config == null)
            {
                completionCallback?.Invoke(false, "No configuration provided");
                yield break;
            }

            // Get all external wit entities
            WitAppInfo appInfo = _config.GetApplicationInfo();
            WitEntityInfo[] externalEntities = appInfo.entities;
            if (externalEntities == null)
            {
                externalEntities = new WitEntityInfo[0];
            }

            // Create all missing local entities
            var localEntityNames = manifest.Entities.Select(localEntity => localEntity.ID).ToHashSet();
            foreach (var externalEntity in externalEntities)
            {
                string entityName = ConduitUtilities.GetEntityEnumName(externalEntity.name);
                if (!localEntityNames.Contains(entityName))
                {
                    CreateEnumFromWitEntity(externalEntity);
                    yield return null;
                }
            }

            // Import newly generated entities
            AssetDatabase.Refresh();

            // Sync all local entities
            bool allEntitiesSynced = true;
            var externalEntityNames = externalEntities.Select(externalEntity => ConduitUtilities.GetEntityEnumName(externalEntity.name)).ToList();
            foreach (var localEntity in manifest.Entities)
            {
                // Get entity final name
                int externalIndex = externalEntityNames.IndexOf(localEntity.ID);

                // Sync
                yield return Sync(externalIndex != -1 ? externalEntities[externalIndex] : new WitEntityInfo(), localEntity, (success, data) =>
                {
                    if (!success)
                    {
                        allEntitiesSynced = false;
                    }
                });
            }

            // Import newly generated entities
            AssetDatabase.Refresh();

            // Complete
            completionCallback(allEntitiesSynced, null);
        }

        // Creates enum file for wit entity
        private void CreateEnumFromWitEntity(WitEntityInfo entityInfo)
        {
            // Generate wrapper
            var wrapper = new EnumCodeWrapper(_fileIo, ConduitUtilities.GetEntityEnumName(entityInfo.name));

            // Update wrapper
            UpdateEnumWrapper(wrapper, entityInfo.name, entityInfo.id, entityInfo.keywords);

            // Write to file
            wrapper.WriteToFile();
        }

        /// <summary>
        /// Synchronizes an enum with its corresponding Wit.Ai entity.
        /// </summary>
        /// <param name="completionCallback">The callback to call when the sync operation is complete.</param>
        private IEnumerator Sync(WitEntityInfo externalEntityInfo, ManifestEntity localEntityInfo, StepResult completionCallback)
        {
            // Add missing
            var result = false;
            if (string.IsNullOrEmpty(externalEntityInfo.id))
            {
                // Create entity on wit
                yield return CreateEntityOnWit(localEntityInfo,
                    (newEntity, success, data) =>
                    {
                        externalEntityInfo = newEntity;
                        result = success;
                    });

                // Return if failed
                if (!result)
                {
                    completionCallback(false, $"Failed to create new entity {localEntityInfo.ID} on Wit.Ai");
                    yield break;
                }
            }

            // Get delta
            var delta = GetDelta(externalEntityInfo, localEntityInfo);

            // Add missing values to wit
            yield return AddValuesToWit(externalEntityInfo, delta,
                (success, data) => result = success);

            // Failed to add to wit
            if (!result)
            {
                completionCallback(false, "Failed to add values to Wit.Ai");
                yield break;
            }

            // Add to local
            if (!AddValuesToLocalEnum(externalEntityInfo, localEntityInfo, delta))
            {
                completionCallback(false, "Failed to add entity to local enum");
                yield break;
            }

            // Successful sync
            completionCallback(true, "");
        }

        /// <summary>
        /// Returns the entries that are in one or the other.
        /// </summary>
        private EntitiesDelta GetDelta(WitEntityInfo externalEntityInfo, ManifestEntity localEntityInfo)
        {
            var delta = new EntitiesDelta();

            var manifestEntityValues = new HashSet<string>();
            foreach (var value in localEntityInfo.Values)
            {
                manifestEntityValues.Add(value);
            }

            if (externalEntityInfo.keywords == null || externalEntityInfo.keywords.Length == 0)
            {
                delta.InLocalOnly = manifestEntityValues.ToList();
                delta.InWitOnly = new List<WitEntityKeywordInfo>();

                return delta;
            }

            var witEntityValues = new HashSet<string>();

            foreach (var keyword in externalEntityInfo.keywords)
            {
                witEntityValues.Add(keyword.keyword);
            }

            var originalWitValues = witEntityValues.ToList();
            witEntityValues.ExceptWith(manifestEntityValues);
            manifestEntityValues.ExceptWith(originalWitValues);

            delta.InLocalOnly = manifestEntityValues.ToList();
            delta.InWitOnly = externalEntityInfo.keywords.ToList().FindAll(keyword => witEntityValues.Contains(keyword.keyword));

            return delta;
        }

        /// <summary>
        /// Perform web request to add a wit entity
        /// </summary>
        private IEnumerator CreateEntityOnWit(ManifestEntity localEntity, Action<WitEntityInfo, bool, string> completionCallback)
        {
            // Get outgoing entity
            WitEntityInfo outgoingEntity = new WitEntityInfo()
            {
                name = localEntity.ID
            };
            List<WitEntityKeywordInfo> keywords = new List<WitEntityKeywordInfo>();
            foreach (var value in localEntity.Values)
            {
                keywords.Add(new WitEntityKeywordInfo()
                {
                    keyword = value,
                    synonyms = new List<string>(new[]{value})
                });
            }
            outgoingEntity.keywords = keywords.ToArray();

            // Create entity
            VLog.D($"Create entity on Wit.ai\nEntity: {outgoingEntity.name}");

            // Get new external entity
            WitEntityInfo incomingEntity = new WitEntityInfo();
            bool running = true;
            WitEditorRequestUtility.AddEntity(_config, outgoingEntity, null, (newEntity, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    VLog.W($"Entity generation failed\nEntity: {outgoingEntity.name}\n\n{error}");
                }
                incomingEntity = newEntity;
                running = false;
            });
            yield return new WaitWhile(() => running);
            completionCallback(incomingEntity, !string.IsNullOrEmpty(incomingEntity.id), "");
        }

        /// <summary>
        /// Perform request to add all local only keywords
        /// </summary>
        private IEnumerator AddValuesToWit(WitEntityInfo entityInfo,
            EntitiesDelta delta, StepResult completionCallback)
        {
            var allSuccessful = true;
            foreach (var keyword in delta.InLocalOnly)
            {
                bool running = true;
                VLog.D($"Add entity keyword on Wit.ai\nEntity: {entityInfo.name}\nKeyword: {keyword}");
                WitEditorRequestUtility.AddEntityKeyword(_config, entityInfo.id, keyword, new [] {keyword}, null, (error) =>
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        allSuccessful = false;
                        VLog.W($"Entity keyword addition failed\nEntity: {entityInfo.id}\nKeyword: {keyword}\n\n{error}");
                    }
                    running = false;
                });
                yield return new WaitWhile(() => running);
            }
            completionCallback(allSuccessful, "");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="manifestEntity"></param>
        /// <param name="delta"></param>
        /// <returns></returns>
        private bool AddValuesToLocalEnum(WitEntityInfo entityInfo, ManifestEntity manifestEntity,
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
            // Add namespace
            UpdateEnumWrapper(enumWrapper, entityInfo.name, entityInfo.id, delta.InWitOnly.ToArray());

            // Write to file
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

            EnumCodeWrapper wrapper = new EnumCodeWrapper(_fileIo, manifestEntity.ID, null, sourceFile);

            // Get enum values
            Type valueAttribute = typeof(ConduitValueAttribute);
            CodeTypeReference entityKeywordAttributeType = new CodeTypeReference(valueAttribute.Name);
            foreach (var enumName in enumType.GetEnumNames())
            {
                // Synonyms
                string keyword = enumName;
                List<string> synonyms = new List<string>();

                // Get member
                var memberInfo = enumType.GetMember(enumName);
                var enumValueMemberInfo = memberInfo.FirstOrDefault(m => m.DeclaringType == enumType);
                if (enumValueMemberInfo != null)
                {
                    foreach (var enumAttribute in (ConduitValueAttribute[])enumValueMemberInfo.GetCustomAttributes(valueAttribute, false))
                    {
                        if (enumAttribute.Aliases != null)
                        {
                            synonyms.AddRange(enumAttribute.Aliases);
                        }
                    }
                    if (synonyms.Count > 0)
                    {
                        keyword = synonyms[0];
                    }
                }

                // Add existing
                AddEnumValueAttribute(wrapper, entityKeywordAttributeType, keyword, synonyms);
            }

            // Return wrapper
            return wrapper;
        }

        // Update enum wrapper
        private void UpdateEnumWrapper(EnumCodeWrapper wrapper, string entityName, string entityId, WitEntityKeywordInfo[] entityKeywords)
        {
            // Add namespace
            wrapper.AddNamespaceImport(typeof(ConduitEntityAttribute));

            // Add entity enum attribute
            CodeTypeReference entityAttributeType = new CodeTypeReference(typeof(ConduitEntityAttribute).Name);
            CodeAttributeArgument[] entityAttributeArgs = new CodeAttributeArgument[]
            {
                new CodeAttributeArgument(new CodePrimitiveExpression(entityName)),
                new CodeAttributeArgument(new CodePrimitiveExpression(entityId))
            };
            wrapper.AddEnumAttribute(new CodeAttributeDeclaration(entityAttributeType, entityAttributeArgs));

            // Add entity keywords & their enum attributes
            if (entityKeywords != null)
            {
                CodeTypeReference entityKeywordAttributeType = new CodeTypeReference(typeof(ConduitValueAttribute).Name);
                foreach (var keyword in entityKeywords)
                {
                    AddEnumValueAttribute(wrapper, entityKeywordAttributeType, keyword.keyword, keyword.synonyms);
                }
            }
        }
        // Add enum value
        private void AddEnumValueAttribute(EnumCodeWrapper wrapper, CodeTypeReference entityKeywordAttributeType, string keyword, IList<string> synonyms)
        {
            // Get clean keyword
            string cleanKeyword = ConduitUtilities.GetEntityEnumValue(keyword);

            // Add unique synonyms
            List<string> synonymList = new List<string>();
            synonymList.Add(keyword);
            if (synonyms != null)
            {
                foreach (var synonym in synonyms)
                {
                    if (!synonymList.Contains(synonym))
                    {
                        synonymList.Add(synonym);
                    }
                }
            }

            // Convert to arguments
            CodeAttributeArgument[] entityKeywordAttributeArgs = synonymList.ConvertAll((synonym) =>
                new CodeAttributeArgument(new CodePrimitiveExpression(synonym))).ToArray();

            // Add value
            wrapper.AddValue(cleanKeyword, new CodeAttributeDeclaration(entityKeywordAttributeType, entityKeywordAttributeArgs));
        }
    }
}
