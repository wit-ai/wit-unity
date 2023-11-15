/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JetBrains.Annotations;

namespace Meta.WitAi
{
    /// <summary>
    /// This is a export parser which will save the current wit export to file
    /// if it hasn't already been saved.
    /// </summary>
    [UsedImplicitly]
    public class ExportSaver : IExportParserPlugin
    {
        /// <summary>
        /// The folder where the wit exports will be saved.
        /// </summary>
        /// <remarks>This will eventually be user configurable</remarks>
        private static readonly string ExportSubFolder = "ProjectSettings/WitConfigExports/";

        public void Process(IWitRequestConfiguration config, ZipArchive zipArchive)
        {
            string appId = config.GetApplicationId();

            if (AlreadyExists(zipArchive, appId)) return;

            string name = CreateExportFilename(config);
            SaveArchiveToFile(zipArchive, name);
        }

        /// <summary>
        /// Generates the name to be used for the backup.
        /// </summary>
        /// <param name="config">the config data to be used for the export</param>
        /// <returns>A timestamped name relative to the config provided</returns>
        private static string CreateExportFilename(IWitRequestConfiguration config)
        {
            return $"{config.GetApplicationId()}_{config.GetApplicationInfo().name}_{DateTime.Now:yyyyMMdd_HHmmss}.zip";
        }

        /// <summary>
        /// Saves the given export archive to the given path.
        /// </summary>
        private static void SaveArchiveToFile(ZipArchive zipArchive, string appId)
        {
            string path = ExportSubFolder + appId;

            // Create a new file stream
            using FileStream fileStream = new FileStream(path, FileMode.Create);
            // Create a new ZipArchive to write to the file stream
            using ZipArchive newZipArchive = new ZipArchive(fileStream, ZipArchiveMode.Create);
            foreach (ZipArchiveEntry entry in zipArchive.Entries)
            {
                ZipArchiveEntry newEntry = newZipArchive.CreateEntry(entry.FullName);
                using Stream originalEntryStream = entry.Open();
                using Stream newEntryStream = newEntry.Open();
                originalEntryStream.CopyTo(newEntryStream);
            }
        }

        /// <summary>
        /// Checks if the given archive has already been saved.
        /// </summary>
        /// <param name="archive">the archive in question</param>
        /// <param name="appId">the unique app ID of the export archive</param>
        /// <returns>true if it has been saved, false otherwise</returns>
        private static bool AlreadyExists(ZipArchive archive, string appId)
        {
            if (!Directory.Exists(ExportSubFolder))
            {
                Directory.CreateDirectory(ExportSubFolder);
            }

            // Get a list of files in the directory that match the known prefix
            string[] files = Directory.GetFiles(ExportSubFolder, $"{appId}*")
                .ToArray();

            if (files.Length == 0)
            {
                return false;
            }

            // Sort the files by creation time (most recent first)
            string mostRecentFile = files.OrderByDescending(File.GetLastWriteTime).First();

            // Open the .zip file for reading
            using ZipArchive zip = ZipFile.OpenRead(mostRecentFile);
            return archive.IsEqual(zip);
        }
    }
}
