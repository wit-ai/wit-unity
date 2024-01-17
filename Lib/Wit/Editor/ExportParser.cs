/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Meta.WitAi.Json;

namespace Meta.WitAi
{

    /// <summary>
    /// Parses the Wit.ai Export zip file
    /// </summary>
    public class ExportParser : PluggableBase<IExportParserPlugin>
    {
        public ExportParser()
        {
            EnsurePluginsAreLoaded();
        }

        /// <summary>
        /// Finds all the Json files canvases in the zip archive under the given folder
        /// </summary>
        /// <returns>new list of entries which represent json files</returns>
        public List<ZipArchiveEntry> GetJsonFileNames(string folder, ZipArchive zip)
        {
            var jsonCanvases = new List<ZipArchiveEntry>();
            foreach (var entry in zip.Entries)
            {
                if (entry.FullName.Contains(folder))
                {
                    jsonCanvases.Add(entry);
                }
            }
            return jsonCanvases;
        }

        /// <summary>
        /// Extracts a Wit JSON object representing the given json file
        /// </summary>
        /// <param name="zip">zip archive from Wit.ai export</param>
        /// <param name="fileName">one of the file names</param>
        /// <returns>The entire canvas structure as nested JSON objects</returns>
        public WitResponseNode ExtractJson(ZipArchive zip, string fileName)
        {
            var entry = zip.Entries.First((v) => v.Name.EndsWith(fileName));
            if (entry.Name.EndsWith(fileName))
            {
                var stream = entry.Open();
                var json = new StreamReader(stream).ReadToEnd();

                return JsonConvert.DeserializeToken(json);
            }
            VLog.W("Could not open file named "+ fileName);
            return null;
        }

        /// <summary>
        /// Calls Process on all IExportParserPlugin objects within the project's
        /// loaded assemblies
        /// </summary>
        /// <param name="config">the config to pass to the Process function</param>
        /// <param name="zip">the zip archive to pass ot the Process function</param>
        public void ProcessExtensions(IWitRequestConfiguration config, ZipArchive zip)
        {
            EnsurePluginsAreLoaded();
            foreach (IExportParserPlugin plugin in LoadedPlugins)
            {
                plugin.Process(config, zip);
            }
        }
    }
}
