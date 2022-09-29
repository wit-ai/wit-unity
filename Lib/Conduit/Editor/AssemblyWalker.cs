/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEditor.Compilation;
using Assembly = UnityEditor.Compilation.Assembly;

namespace Meta.Conduit.Editor
{
    /// <summary>
    /// This class is responsible for scanning assemblies for relevant Conduit data.
    /// </summary>
    internal class AssemblyWalker : IAssemblyWalker
    {
        /// <summary>
        /// The assembly that code not within an assembly is added to
        /// </summary>
        public const string DEFAULT_ASSEMBLY_NAME = "Assembly-CSharp";

        /// <inheritdoc/>
        public HashSet<string> AssembliesToIgnore { get; set; } = new HashSet<string>();

        /// <inheritdoc/>
        public IEnumerable<IConduitAssembly> GetAllAssemblies()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.IsDefined(typeof(ConduitAssemblyAttribute)) || string.Equals(DEFAULT_ASSEMBLY_NAME, assembly.GetName().Name));
            return assemblies.Select(assembly => new ConduitAssembly(assembly)).ToList();
        }

        /// <inheritdoc/>
        public IEnumerable<IConduitAssembly> GetTargetAssemblies()
        {
            if (AssembliesToIgnore != null && AssembliesToIgnore.Count() > 0) {
                return GetAllAssemblies().Where(assembly => !AssembliesToIgnore.Contains(assembly.FullName));
            }
            return GetAllAssemblies();
        }

        /// <inheritdoc/>
        public IEnumerable<Assembly> GetCompilationAssemblies(AssembliesType assembliesType)
        {
            return CompilationPipeline.GetAssemblies(assembliesType);
        }

        public bool GetSourceCode(Type type, out string sourceCodeFile)
        {
            if (type == null || !type.IsEnum)
            {
                throw new ArgumentException("Type needs to be an enum");
            }

            foreach (var assembly in GetCompilationAssemblies(AssembliesType.Player))
            {
                if (GetSourceCodeFromAssembly(assembly, type, out sourceCodeFile))
                {
                    return true;
                }
            }

            sourceCodeFile = string.Empty;
            return false;
        }

        private bool GetSourceCodeFromAssembly(Assembly assembly, Type type, out string sourceCodeFile)
        {
            // TODO: Cache code files.
            var defaultFileName = GetDefaultFileName(type);

            foreach (var sourceFile in assembly.sourceFiles)
            {
                if (!sourceFile.EndsWith(defaultFileName) || (!ContainsType(sourceFile, type)))
                {
                    continue;
                }

                sourceCodeFile = sourceFile;
                return true;
            }

            sourceCodeFile = string.Empty;
            return false;
        }

        private bool ContainsType(string sourceFile, Type type)
        {
            if (!type.IsEnum)
            {
                throw new ArgumentException("Type needs to be an enum");
            }

            var pattern = $"{{enum}}\\s{type.Name}";
            var text = File.ReadAllText(sourceFile);

            return Regex.IsMatch(text, pattern);
        }

        private string GetDefaultFileName(Type type)
        {
            return $"{type.Name}.cs";
        }
    }
}
