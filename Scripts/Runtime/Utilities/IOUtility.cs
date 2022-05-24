/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */


using System;
using System.IO;
using UnityEngine;

namespace Facebook.WitAi.Utilities
{
    public static class IOUtility
    {
        // Log error
        private static void LogError(string error)
        {
            Debug.LogError($"IO Utility - {error}");
        }

        /// <summary>
        /// Creates a directory recursively if desired and returns true if successful
        /// </summary>
        /// <param name="directoryPath">The directory to be created</param>
        /// <param name="recursively">Will traverse parent directories if needed</param>
        /// <returns>Returns true if the directory exists</returns>
        public static bool CreateDirectory(string directoryPath, bool recursively = true)
        {
            // Already exists
            if (Directory.Exists(directoryPath))
            {
                return true;
            }

            // Check parent
            string parentDirectoryPath = Path.GetDirectoryName(directoryPath);
            Debug.Log($"Check Parent\nDir: {directoryPath}\nParent: {parentDirectoryPath}");
            if (!Directory.Exists(parentDirectoryPath))
            {
                // Not allowed
                if (!recursively)
                {
                    LogError($"Cannot Create Directory\nDirectory Path: {directoryPath}");
                    return false;
                }
                // Failed in parent
                else if (!CreateDirectory(parentDirectoryPath, recursively))
                {
                    return false;
                }
            }

            try
            {
                Directory.CreateDirectory(directoryPath);
            }
            catch (Exception e)
            {
                LogError($"Create Directory Exception\nDirectory Path: {directoryPath}\n{e}");
                return false;
            }

            // Successfully created
            return true;
        }

        /// <summary>
        /// Deletes a directory and returns true if the directory no longer exists
        /// </summary>
        /// <param name="directoryPath">The directory to be created</param>
        /// <param name="forceIfFilled">Whether to force a deletion if the directory contains contents</param>
        /// <returns>Returns true if the directory does not exist</returns>
        public static bool DeleteDirectory(string directoryPath, bool forceIfFilled = true)
        {
            // Already gone
            if (!Directory.Exists(directoryPath))
            {
                return true;
            }

            try
            {
                Directory.Delete(directoryPath, forceIfFilled);
            }
            catch (Exception e)
            {
                LogError($"Delete Directory Exception\nDirectory Path: {directoryPath}\n{e}");
                return false;
            }

            // Successfully deleted
            return true;
        }
    }
}
