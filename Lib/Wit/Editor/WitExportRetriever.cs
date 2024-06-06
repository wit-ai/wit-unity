/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using Meta.WitAi.Data.Info;
using Meta.WitAi.Requests;
namespace Meta.WitAi
{
    /// <summary>
    /// A class to synchronize multiple parallel retrievals of the same app's export.
    /// </summary>
    internal abstract class WitExportRetriever
    {
        /// <summary>
        /// Retrieves the export for the requested configuration and calls the onComplete once retrieved.
        /// </summary>
        /// <param name="configuration">the config of the app export to be retrieved</param>
        /// <returns>Returns the zip archive if successful</returns>
        public static async Task<VRequestResponse<ZipArchive>> GetExport(IWitRequestConfiguration configuration)
        {
            // Fail without
            string appId = configuration.GetApplicationId();
            if (string.IsNullOrEmpty(appId))
            {
                return new VRequestResponse<ZipArchive>(WitConstants.ERROR_CODE_GENERAL, "Does not yet have app id");
            }

            // Get export info
            var exportInfoResult = await new WitInfoVRequest(configuration).RequestAppExportInfo(appId);
            if (!string.IsNullOrEmpty(exportInfoResult.Error))
            {
                return new VRequestResponse<ZipArchive>(WitConstants.ERROR_CODE_GENERAL, exportInfoResult.Error);
            }

            // Get export file
            var exportFileResult = await new VRequest().RequestFile(exportInfoResult.Value.uri);
            if (!string.IsNullOrEmpty(exportInfoResult.Error))
            {
                return new VRequestResponse<ZipArchive>(WitConstants.ERROR_CODE_GENERAL, exportInfoResult.Error);
            }

            // Generate zip archive
            var zip = new ZipArchive(new MemoryStream(exportFileResult.Value));

            // now call any hooks connected to a completed export download
            new ExportParser().ProcessExtensions(configuration, zip);

            // Return if successful
            return new VRequestResponse<ZipArchive>(zip);
        }
    }
}
