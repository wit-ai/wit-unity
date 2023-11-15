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
using Meta.WitAi.Data.Info;
using Meta.WitAi.Requests;
namespace Meta.WitAi
{
    /// <summary>
    /// A class to synchronize multiple parallel retrievals of the same app's export.
    /// </summary>
    public abstract class WitExportRetriever
    {
        /// <summary>
        /// Tracks which callbacks have already been registered for a specific export retrieval
        /// this is necessary as the equality checks on delegates don't work
        /// </summary>
        private static readonly Dictionary<string, List<MethodInfo>> CallbacksPerConfig =  new Dictionary<string, List<MethodInfo>>();

        //tracks the delegates to call for each config
        private static readonly Dictionary<string, List<VRequest.RequestCompleteDelegate<ZipArchive>>> PendingCallbacksPerConfig =  new Dictionary<string, List<VRequest.RequestCompleteDelegate<ZipArchive>>>();

        /// <summary>
        /// Retrieves the export for the requested configuration and calls the onComplete once retrieved.
        /// </summary>
        /// <param name="configuration">the config of the app export to be retrieved</param>
        /// <param name="onComplete">the function to call upon successful retrieval</param>
        public static void GetExport(IWitRequestConfiguration configuration, VRequest.RequestCompleteDelegate<ZipArchive> onComplete )
        {
            string appId = configuration.GetApplicationId();
            if (string.IsNullOrEmpty(appId)) return; //new config; haven't yet retrieved it.
            if (!CallbacksPerConfig.ContainsKey(appId))
            {
                CallbacksPerConfig[appId] = new List<MethodInfo>();
                PendingCallbacksPerConfig[appId] = new List<VRequest.RequestCompleteDelegate<ZipArchive>>();
            }

            if (CallbacksPerConfig[appId].Contains(onComplete.Method)) return;

            PendingCallbacksPerConfig[appId].Add(onComplete);
            CallbacksPerConfig[appId].Add(onComplete.Method);

            if (PendingCallbacksPerConfig[appId].Count == 1)
            {
                new WitInfoVRequest(configuration, true).RequestAppExportInfo((exportInfo, exportInfoError) =>
                        OnExportInfoGetCompletion(configuration, exportInfo, exportInfoError));
            }
        }
        /// <summary>
        /// Callback following Wit request for app info
        /// </summary>
        /// <param name="appId">Id of hte app being exported</param>
        /// <param name="exportInfo">the info about what export to download</param>
        /// <param name="exportInfoError">any errors which may have occurred during export; may be null or empty</param>
        private static void OnExportInfoGetCompletion(IWitRequestConfiguration configuration, WitExportInfo exportInfo, string exportInfoError)
        {
            if (!string.IsNullOrEmpty(exportInfoError))
            {
                OnExportZipLoadCompletion(configuration, null, exportInfoError);
                return;
            }
            new VRequest().RequestFile(new Uri(exportInfo.uri),
                (exportZipData, exportZipError) =>
                    OnExportZipLoadCompletion(configuration, exportZipData, exportInfoError));
        }

        /// <summary>
        /// Callback following zip file request
        /// </summary>
        /// <param name="appConfig">app ID which was exported</param>
        /// <param name="exportZipData">the raw data which was downloaded</param>
        /// <param name="exportZipError">string of any errors which may have occurred</param>
        private static void OnExportZipLoadCompletion(IWitRequestConfiguration appConfig, byte[] exportZipData, string exportZipError)
        {
            if (!string.IsNullOrEmpty(exportZipError))
            {
                OnExportZipDecodeCompletion(appConfig, null, exportZipError);
                return;
            }
            try
            {
                var zip = new ZipArchive(new MemoryStream(exportZipData));
                OnExportZipDecodeCompletion(appConfig, zip, null);
            }
            catch (Exception e)
            {
                OnExportZipDecodeCompletion(appConfig, null, e.ToString());
            }
        }

        /// <summary>
        /// Callback following final export completion
        /// </summary>
        /// <param name="appId">app ID which was exported</param>
        /// <param name="result">the formatted and decoded Zip archive which was downloaded</param>
        /// <param name="error">the errors which occurred, if any</param>
        private static void OnExportZipDecodeCompletion(IWitRequestConfiguration appConfig, ZipArchive result, string error)
        {
            string appId = appConfig.GetApplicationId();
            foreach (var pending in PendingCallbacksPerConfig[appId])
            {
                pending.Invoke(result, error);
            }
            PendingCallbacksPerConfig[appId].Clear();
            CallbacksPerConfig[appId].Clear();

            if (!string.IsNullOrEmpty(error))
                return;

            // now call any hooks connected to a completed export download
            new ExportParser().ProcessExtensions(appConfig ,result);
        }
    }
}
