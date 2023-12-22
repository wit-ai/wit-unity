/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using UnityEngine.Networking;
using UnityEngine.Scripting;

namespace Meta.WitAi.Requests
{
    /// <summary>
    /// A download handler for UnityWebRequest that decodes text data
    /// as it is received and returns it via a partial response delegate.
    /// </summary>
    public class TextStreamHandler : DownloadHandlerScript, IRequestDownloadHandler
    {
        /// <summary>
        /// The delegate for returning text from the text stream handler
        /// </summary>
        public delegate void TextStreamResponseDelegate(string rawText);

        /// <summary>
        /// The callback when a partial delimiter is found
        /// </summary>
        private TextStreamResponseDelegate _partialResponseDelegate;

        /// <summary>
        /// When this text is found within the incoming stream, a response
        /// is immediately returned via the partial response delegate
        /// </summary>
        private string _partialDelimiter = DEFAULT_PARTIAL_DELIMITER;

        /// <summary>
        /// The default partial delimiter
        /// </summary>
        public const string DEFAULT_PARTIAL_DELIMITER = "\r\n";

        /// <summary>
        /// The text added to the final builder in between partials
        /// </summary>
        private string _finalDelimiter = DEFAULT_FINAL_DELIMITER;

        /// <summary>
        /// The default final delimiter
        /// </summary>
        public const string DEFAULT_FINAL_DELIMITER = "\n";

        /// <summary>
        /// String builder used for partial response callbacks
        /// </summary>
        private StringBuilder _partialBuilder = new StringBuilder();

        /// <summary>
        /// String builder used for final response callbacks
        /// </summary>
        private StringBuilder _finalBuilder = new StringBuilder();

        // Final length of text
        private int _finalLength;

        /// <summary>
        /// Whether or not complete
        /// </summary>
        public bool IsComplete { get; private set; } = false;

        // Generate with a specified delimiter
        [Preserve]
        public TextStreamHandler(TextStreamResponseDelegate partialResponseDelegate, string partialDelimiter = DEFAULT_PARTIAL_DELIMITER, string finalDelimiter = DEFAULT_FINAL_DELIMITER)
        {
            _partialResponseDelegate = partialResponseDelegate;
            _partialDelimiter = partialDelimiter;
            _finalDelimiter = finalDelimiter;
        }

        // Receive data
        [Preserve]
        protected override bool ReceiveData(byte[] receiveData, int dataLength)
        {
            // Convert to text
            string newText = DecodeBytes(receiveData, 0, dataLength);
            // Split on delimiter
            string[] chunks = SplitText(newText, _partialDelimiter);
            // Iterate chunks
            for (int i = 0; i < chunks.Length; i++)
            {
                // Get chunk
                string chunk = chunks[i];
                // Ignore empty chunk
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }

                // Append new chunk
                _partialBuilder.Append(chunk);

                // Handle partial
                if (i < chunks.Length - 1)
                {
                    HandlePartial(_partialBuilder.ToString());
                    _partialBuilder.Clear();
                }
            }
            // Return data
            return true;
        }

        // Handle partial text
        protected virtual void HandlePartial(string newPartial)
        {
            // Call partial delegate
            _partialResponseDelegate?.Invoke(newPartial);
            // Append delimiter if possible
            if (_finalBuilder.Length > 0)
            {
                _finalBuilder.Append(_finalDelimiter);
            }
            // Append new partial
            _finalBuilder.Append(newPartial);
        }

        // Returns the full text response
        [Preserve]
        protected override string GetText()
        {
            return _finalBuilder.ToString() +
                   (_finalBuilder.Length > 0 && _partialBuilder.Length > 0 ? _finalDelimiter : "") +
                   _partialBuilder.ToString();
        }

        // Stores the content length for progress determination
        [Preserve]
        protected override void ReceiveContentLengthHeader(ulong contentLength)
        {
            base.ReceiveContentLengthHeader(contentLength);
            _finalLength = GetDecodedLength(contentLength);
        }

        // Return progress if total samples has been determined
        [Preserve]
        protected override float GetProgress()
        {
            if (IsComplete)
            {
                return 1f;
            }
            if (_finalLength > 0)
            {
                return (float)(_partialBuilder.Length + _finalBuilder.Length) / _finalLength;
            }
            return 0f;
        }

        [Preserve]
        protected override byte[] GetData()
        {
            return Encoding.UTF8.GetBytes(_finalBuilder.ToString());
        }

        // Clean up clip with final sample count
        [Preserve]
        protected override void CompleteContent()
        {
            if (_partialBuilder.Length > 0)
            {
                HandlePartial(_partialBuilder.ToString());
                _partialBuilder.Clear();
            }
            IsComplete = true;
        }

        #region STATIC
        /// <summary>
        /// Simple decode method from bytes to text
        /// </summary>
        /// <param name="receiveData">The data received</param>
        /// <param name="start">The array start index</param>
        /// <param name="length">The amount of bytes in the data to convert</param>
        /// <returns>Returns decoded text</returns>
        public static string DecodeBytes(byte[] receiveData, int start, int length) =>
            Encoding.UTF8.GetString(receiveData, start, length);

        /// <summary>
        /// Gets the length of the string that should be created with the total amount of bits
        /// </summary>
        /// <param name="totalBits">The total amount of bits</param>
        /// <returns>Returns string length of decoded bytes</returns>
        public static int GetDecodedLength(ulong totalBits) => (int)(totalBits / 8);

        /// <summary>
        /// Splits text with a string delimiter
        /// </summary>
        /// <param name="source">Original source text</param>
        /// <param name="delimiter">Delimiter to be split on</param>
        /// <returns>The source string split on the delimiter</returns>
        public static string[] SplitText(string source, string delimiter)
        {
            #if UNITY_2021_1_OR_NEWER
            return source.Split(delimiter);
            #else
            var results = new System.Collections.Generic.List<string>();
            var temp = source;
            int index = temp.IndexOf(delimiter);
            while (index >= 0)
            {
                results.Add(temp.Substring(0, index));
                temp = temp.Substring(index + delimiter.Length);
                index = temp.IndexOf(delimiter);
            }
            results.Add(temp);
            return results.ToArray();
            #endif
        }
        #endregion

        #region TESTS
        // For internal testing, allows inserting of data for ReceiveData method
        internal bool ReceiveData(byte[] receiveData) => ReceiveData(receiveData, receiveData.Length);

        // For internal testing, allows manual completion
        internal void Complete() => CompleteContent();
        #endregion TESTS
    }
}
