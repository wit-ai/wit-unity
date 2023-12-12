/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.WitAi.TTS.Data;
using UnityEngine;
using UnityEngine.UI;
using Meta.WitAi.TTS.Utilities;

namespace Meta.WitAi.TTS.UX
{
    /// <summary>
    /// A simple script to be added to a label that will update for any
    /// TTS related issues.
    /// </summary>
    public class TTSSpeakerErrorLabel : TTSSpeakerObserver
    {
        /// <summary>
        /// The label to be updated
        /// </summary>
        [SerializeField] private Text _errorLabel;

        /// <summary>
        /// The current error response
        /// </summary>
        private string _lastError;
        /// <summary>
        /// The last load error
        /// </summary>
        private string _lastLoadError;

        /// <summary>
        /// On awake, obtain error
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            if (_errorLabel == null)
            {
                _errorLabel = gameObject.GetComponentInChildren<Text>();
            }
        }

        /// <summary>
        /// Refresh error on enable
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshError();
        }

        /// <summary>
        /// Clear load error on new request
        /// </summary>
        protected override void OnLoadBegin(TTSSpeaker speaker, TTSClipData clipData)
        {
            base.OnLoadBegin(speaker, clipData);
            _lastLoadError = null;
            RefreshError();
        }

        /// <summary>
        /// Set load error on fail
        /// </summary>
        protected override void OnLoadFailed(TTSSpeaker speaker, TTSClipData clipData, string error)
        {
            base.OnLoadFailed(speaker, clipData, error);
            _lastLoadError = error;
            RefreshError();
        }

        /// <summary>
        /// Refreshes the current error
        /// </summary>
        public void RefreshError()
        {
            // Get current error
            string error = GetCurrentError();
            if (string.Equals(_lastError, error, StringComparison.CurrentCulture))
            {
                return;
            }

            // Set text
            _lastError = error;

            // Apply to label
            if (_errorLabel)
            {
                if (string.IsNullOrEmpty(_lastError))
                {
                    _errorLabel.text = string.Empty;
                }
                else
                {
                    _errorLabel.text = $"Error: {_lastError}";
                }
            }
        }

        /// <summary>
        /// Get the current error if applicable
        /// </summary>
        public string GetCurrentError()
        {
            if (Speaker == null)
            {
                return "No TTS Speaker found";
            }
            if (Speaker.TTSService == null)
            {
                return "No TTS Service found on speaker";
            }
            string error = Speaker.TTSService.GetInvalidError();
            if (!string.IsNullOrEmpty(error))
            {
                return error;
            }
            if (!string.IsNullOrEmpty(_lastLoadError))
            {
                return _lastLoadError;
            }
            return string.Empty;
        }
    }
}
