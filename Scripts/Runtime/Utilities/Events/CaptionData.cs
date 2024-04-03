/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

namespace Meta.WitAi.Utilities
{
    /// <summary>
    /// Represents the caption data that would be spoked by TTS.
    /// </summary>
    public class CaptionData {
        /// <summary>
        /// Text ready to be sent to TTS speaker.
        /// </summary>
        public string Text;

        /// <summary>
        /// Text that can be displayed in a text field.
        /// </summary>
        public string DisplayText;

        /// <summary>
        /// The request ID that generated this caption.
        /// </summary>
        public string RequestId;
    }
}
