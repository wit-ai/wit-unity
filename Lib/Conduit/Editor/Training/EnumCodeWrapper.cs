/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CSharp;
using UnityEngine;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Wraps around an Enum in code to allow querying and modifying its source code in a single source file.
    /// </summary>
    internal class EnumCodeWrapper
    {
        public const string DEFAULT_PATH = @"Assets\";
        public const string DEFAULT_NAMESPACE = "Conduit.Generated";

        private readonly string _sourceFilePath;
        private readonly string _namespace;
        private readonly IFileIo _fileIo;
        private readonly CodeCompileUnit _compileUnit;
        private readonly CodeTypeDeclaration _typeDeclaration;
        private readonly List<string> _enumValues = new List<string>();
        private readonly CodeDomProvider _provider = new CSharpCodeProvider();
        private readonly Dictionary<string, CodeNamespace> _namespaces = new Dictionary<string, CodeNamespace>();
        private readonly Action<CodeNamespace> _namespaceSetup;
        private readonly Action<CodeMemberField> _memberSetup;

        // Setup with existing enum
        public EnumCodeWrapper(IFileIo fileIo, Type enumType, string entityName, string sourceCodeFile) : this(fileIo, enumType.Name, entityName, null, enumType.Namespace, sourceCodeFile)
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Type must be an enumeration.", nameof(enumType));
            }

            var enumValues = new List<WitKeyword>();
            foreach (var enumName in enumType.GetEnumNames())
            {
                // TODO: Read existing synonyms from attributes here.
                enumValues.Add(new WitKeyword()
                {
                    keyword = enumName
                });
            }
            
            AddValues(enumValues);
        }
        
        // Setup
        public EnumCodeWrapper(IFileIo fileIo, string enumName, string entityName, IList<WitKeyword> enumValues, string enumNamespace = "", string sourceCodeFile = "")
        {
            // Initial setup
            _compileUnit = new CodeCompileUnit();
            _namespace = string.IsNullOrEmpty(enumNamespace) ? DEFAULT_NAMESPACE : enumNamespace;
            _sourceFilePath = string.IsNullOrEmpty(sourceCodeFile) ? GetEnumFilePath(enumName, _namespace) : sourceCodeFile;
            _fileIo = fileIo;

            // Setup namespace
            var nameSpace = new CodeNamespace(_namespace);
            _compileUnit.Namespaces.Add(nameSpace);
            _namespaces.Add(_namespace, nameSpace);

            // Setup type declaration
            _typeDeclaration = new CodeTypeDeclaration(enumName)
            {
                IsEnum = true
            };
            nameSpace.Types.Add(_typeDeclaration);

            if (!entityName.Equals(enumName))
            {
                var entityAttributeType = new CodeTypeReference(typeof(ConduitEntityAttribute).Name);
                var entityAttributeArgs = new CodeAttributeArgument[]
                {
                    new CodeAttributeArgument(new CodePrimitiveExpression(entityName))
                };
                this.AddEnumAttribute(new CodeAttributeDeclaration(entityAttributeType, entityAttributeArgs));
            }

            // Add all enum values
            AddValues(enumValues);
        }

        // Ger safe enum file path
        private string GetEnumFilePath(string enumName, string enumNamespace)
        {
            return Path.Combine(DEFAULT_PATH, enumNamespace.Replace('.', '\\'), $"{enumName}.cs");
        }

        // Add namespace import
        public void AddNamespaceImport(Type forType)
        {
            if (forType == null)
            {
                return;
            }
            string attributeNamespaceName = forType.Namespace;
            var importNameSpace = new CodeNamespaceImport(attributeNamespaceName);
            _namespaces[_namespace].Imports.Add(importNameSpace);
        }

        // Add enum attribute
        public void AddEnumAttribute(CodeAttributeDeclaration attribute)
        {
            if (attribute == null)
            {
                return;
            }
            _typeDeclaration.CustomAttributes.Add(attribute);
        }

        /// <summary>
        /// Adds the supplied values to the enum construct. Values that already exist are ignored.
        /// </summary>
        /// <param name="values">The values to add.</param>
        public void AddValues(IList<WitKeyword> values)
        {
            if (values == null)
            {
                return;
            }
            var attributeName = nameof(ConduitValueAttribute);
            var suffix = "Attribute";
            if (attributeName.EndsWith(suffix))
            {
                attributeName = attributeName.Remove(attributeName.Length - suffix.Length);
            }
            
            foreach (var value in values)
            {
                var entityKeywordAttributeType =
                    new CodeTypeReference(attributeName);

                var arguments = new List<CodeAttributeArgument>
                    { new CodeAttributeArgument(new CodePrimitiveExpression(value.keyword)) };

                if (value.synonyms != null)
                {
                    foreach (var synonym in value.synonyms)
                    {
                        if (synonym != value.keyword)
                        {
                            arguments.Add(new CodeAttributeArgument(new CodePrimitiveExpression(synonym)));
                        }
                    }
                }

                AddValue(value.keyword, (arguments.Count >1)?new CodeAttributeDeclaration(entityKeywordAttributeType, arguments.ToArray()):null);
            }
        }
        
        // Add a single value
        public void AddValue(string value, CodeAttributeDeclaration attribute = null)
        {
            // Get clean value
            string cleanValue = ConduitUtilities.SanitizeString(value);

            // Ignore if added
            if (_enumValues.Contains(cleanValue))
            {
                return;
            }

            // Get field
            CodeMemberField field = new CodeMemberField(_typeDeclaration.Name, cleanValue);

            // Add attribute
            if (attribute != null)
            {
                field.CustomAttributes.Add(attribute);
            }

            // Add to enum & members list
            _enumValues.Add(cleanValue);
            _typeDeclaration.Members.Add(field);
        }

        /// <summary>
        /// Removes the supplied values to the enum construct. Values that do not exist in the enum are ignored.
        /// </summary>
        /// <param name="values">The values to remove.</param>
        public void RemoveValues(IList<string> values)
        {
            if (values == null)
            {
                return;
            }
            foreach (var value in values)
            {
                RemoveValue(value);
            }
        }

        /// <summary>
        /// Returns a single value
        /// </summary>
        public void RemoveValue(string value)
        {
            // Check enum names
            string cleanName = ConduitUtilities.SanitizeString(value);
            int enumIndex = _enumValues.IndexOf(cleanName);

            // Not found
            if (enumIndex == -1)
            {
                return;
            }

            // Remove enum
            _enumValues.RemoveAt(enumIndex);
            _typeDeclaration.Members.RemoveAt(enumIndex);
        }

        public void WriteToFile()
        {
            this._fileIo.WriteAllText(_sourceFilePath, this.ToSourceCode());
        }

        public string ToSourceCode()
        {
            // Create a TextWriter to a StreamWriter to the output file.
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                IndentedTextWriter tw = new IndentedTextWriter(sw, "    ");

                // Generate source code using the code provider.
                _provider.GenerateCodeFromCompileUnit(this._compileUnit, tw,
                    new CodeGeneratorOptions()
                    {
                        BracingStyle = "C",
                        BlankLinesBetweenMembers = false,
                        VerbatimOrder = true,
                    });

                // Close the output file.
                tw.Close();
            }

            return sb.ToString();
        }
    }
}
