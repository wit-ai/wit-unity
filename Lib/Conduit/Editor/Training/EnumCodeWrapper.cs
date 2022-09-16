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

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// Wraps around an Enum in code to allow querying and modifying its source code in a single source file.
    /// </summary>
    internal class EnumCodeWrapper
    {
        private readonly string _sourceFilePath;
        private readonly IFileIo _fileIo;
        private readonly CodeCompileUnit _compileUnit;
        private readonly HashSet<string> _enumValues = new HashSet<string>();
        private readonly CodeDomProvider _provider = new CSharpCodeProvider();
        private readonly CodeTypeDeclaration _typeDeclaration = new CodeTypeDeclaration();
        private readonly Dictionary<string, CodeNamespace> _namespaces = new Dictionary<string, CodeNamespace>();
        private readonly string _enumNamespace;

        public EnumCodeWrapper(IFileIo fileIo, Type enumType, string sourceCodeFile = "")
        {
            if (!enumType.IsEnum)
            {
                throw new ArgumentException("Type must be an enumeration.", nameof(enumType));
            }

            _enumNamespace = enumType.Namespace;

            this._compileUnit = new CodeCompileUnit();
            this._sourceFilePath = sourceCodeFile == string.Empty ? GetEnumFileName(enumType.Name) : sourceCodeFile;
            this._fileIo = fileIo;

            var nameSpace = GetNameSpace();

            _typeDeclaration = new CodeTypeDeclaration(enumType.Name)
            {
                IsEnum = true
            };

            foreach (var enumName in enumType.GetEnumNames())
            {
                _enumValues.Add(enumName);
                _typeDeclaration.Members.Add(new CodeMemberField(enumType.Name, enumName));
            }

            nameSpace.Types.Add(_typeDeclaration);
        }

        /// <summary>
        /// Constructs an empty enum wrapper.
        /// This constructor should be used when generating a new file from scratch.
        /// </summary>
        /// <param name="fileIo"></param>
        /// <param name="enumName"></param>
        /// <param name="enumValues"></param>
        /// <param name="sourceCodeFile"></param>
        public EnumCodeWrapper(IFileIo fileIo, string enumName, List<string> enumValues, string sourceCodeDirectory)
        {
            this._compileUnit = new CodeCompileUnit();

            string cleanName = ConduitUtilities.SanitizeName(enumName);
            this._sourceFilePath = $"{sourceCodeDirectory}\\{cleanName}.cs";
            this._fileIo = fileIo;

            const string defaultNameSpace = "Conduit.Generated";
            var nameSpace = new CodeNamespace(defaultNameSpace);
            _compileUnit.Namespaces.Add(nameSpace);
            _namespaces.Add(defaultNameSpace, nameSpace);

            _typeDeclaration = new CodeTypeDeclaration(cleanName)
            {
                IsEnum = true
            };

            foreach (var value in enumValues)
            {
                string cleanValue = ConduitUtilities.SanitizeString(value);
                if (!_enumValues.Contains(cleanValue))
                {
                    _enumValues.Add(cleanValue);
                    _typeDeclaration.Members.Add(new CodeMemberField(cleanName, cleanValue));
                }
            }

            nameSpace.Types.Add(_typeDeclaration);
        }

        private CodeNamespace GetNameSpace()
        {
            if (!_namespaces.ContainsKey(_enumNamespace))
            {
                var newNamespace = new CodeNamespace(_enumNamespace);
                _compileUnit.Namespaces.Add(newNamespace);
                _namespaces.Add(_enumNamespace, newNamespace);
            }

            return _namespaces[_enumNamespace];
        }

        /// <summary>
        /// Adds the supplied values to the enum construct. Values that already exist are ignored.
        /// </summary>
        /// <param name="values">The values to add.</param>
        public void AddValues(IList<string> values)
        {
            foreach (var value in values)
            {
                if (this._enumValues.Contains(value))
                {
                    continue;
                }

                this._typeDeclaration.Members.Add(new CodeMemberField(_typeDeclaration.Name, value));
            }
        }

        /// <summary>
        /// Removes the supplied values to the enum construct. Values that do not exist in the enum are ignored.
        /// </summary>
        /// <param name="values">The values to remove.</param>
        public void RemoveValues(IList<string> values)
        {
            for (var i = this._typeDeclaration.Members.Count - 1; i >= 0; --i)
            {
                if (values.Contains(this._typeDeclaration.Members[i].Name))
                {
                    this._typeDeclaration.Members.RemoveAt(i);
                }
            }
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

        private string GetEnumFileName(string enumName)
        {
            // TODO: Handle the case where the enum already exists.
            const string prefix = @"Assets\Generated\";
            var nameSegments = _enumNamespace.Replace('.', '\\');

            return Path.Combine(prefix, nameSegments, $"{enumName}.cs");
        }

    }
}
