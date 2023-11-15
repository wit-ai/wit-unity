/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Meta.WitAi
{
    /// <summary>
    /// A set of extensions for ZipArchives for easier processing
    /// </summary>
    public static class ZipArchiveExtensions
    {
        /// <summary>
        /// Compares two archives to determine if they're the same.
        /// </summary>
        /// <param name="thisZip">the archive on which this is being called</param>
        /// <param name="otherZip">the other archive against which to compare</param>
        /// <returns>true if they're identical, false otherwise</returns>
        public static bool IsEqual(this ZipArchive thisZip, ZipArchive otherZip)
        {
            // Check if the archives have the same number of entries
            if ((thisZip == null || otherZip == null)
                ||
                (thisZip.Entries.Count != otherZip.Entries.Count))
            {
                return false;
            }
            // Sort the entries by name to ensure they're in the same order
            var entries1 = thisZip.Entries.OrderBy(e => e.FullName).ToList();
            var entries2 = otherZip.Entries.OrderBy(e => e.FullName).ToList();
            // Compare each entry in the archives
            for (int i = 0; i < entries1.Count; i++)
            {
                var entry1 = entries1[i];
                var entry2 = entries2[i];
                // Check if the entries have the same name and length
                if (entry1.FullName != entry2.FullName || entry1.Length != entry2.Length)
                {
                    return false;
                }
                // Check if the entries have the same contents
                using var stream1 = entry1.Open();
                using var stream2 = entry2.Open();
                if (!StreamsAreEqual(stream1, stream2))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Compares two streams to see whether they have the same contents
        /// </summary>
        /// <returns>true if they're byte-wise identical, false otherwise</returns>
        private static bool StreamsAreEqual(Stream stream1, Stream stream2)
        {
            const int bufferSize = 1024 * sizeof(long);
            byte[] buffer1 = new byte[bufferSize];
            byte[] buffer2 = new byte[bufferSize];
            while (true)
            {
                int count1 = stream1.Read(buffer1, 0, bufferSize);
                int count2 = stream2.Read(buffer2, 0, bufferSize);
                if (count1 != count2)
                {
                    return false;
                }
                if (count1 == 0)
                {
                    return true;
                }
                if (!buffer1.SequenceEqual(buffer2))
                {
                    return false;
                }
            }
        }
    }
}
