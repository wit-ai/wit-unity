/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.IO;

namespace Meta.Conduit
{
    internal class FileIo : IFileIo
    {
        public bool Exists(string fileName)
        {
            return File.Exists(fileName);
        }

        public string ReadAllText(string fileName)
        {
            return File.ReadAllText(fileName);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public TextReader OpenText(string fileName)
        {
            return File.OpenText(fileName);
        }
    }
}
