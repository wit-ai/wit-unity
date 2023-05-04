/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Linq;
using UnityEngine;

namespace Meta.WitAi.TTS.Samples
{
    /// <summary>
    /// A demo script that uses a dropdown menu to adjust the
    /// voice setting id of a TTSSpeaker.
    /// </summary>
    public class TTSSpeakerVoiceSelect : TTSSpeakerObserver
    {
        [SerializeField] [Tooltip("Dropdown used for voice selection")]
        private SimpleDropdownList _dropdown;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshDropdown();
            _dropdown.OnOptionSelected.AddListener(OnOptionSelected);
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            _dropdown.OnOptionSelected.RemoveListener(OnOptionSelected);
        }

        // Refresh dropdown using voice settings
        private void RefreshDropdown()
        {
            if (!Speaker)
            {
                VLog.W("No speaker found");
                return;
            }
            if (!Speaker.TTSService)
            {
                VLog.W("No speaker service found");
                return;
            }
            if (!_dropdown)
            {
                VLog.W("No dropdown found");
                return;
            }

            // Get all voice names & load dropdown
            string[] voiceNames = Speaker.TTSService.GetAllPresetVoiceSettings()
                .Select((voiceSetting) => voiceSetting.SettingsId).ToArray();
            _dropdown.LoadDropdown(voiceNames);

            // Get selected voice &
            _dropdown.SelectOption(Speaker.presetVoiceID);
        }

        // Apply voice on option select
        private void OnOptionSelected(string newOption)
        {
            if (!Speaker)
            {
                VLog.W("No speaker found");
                return;
            }
            Debug.Log($"Set Speaker: {newOption}");
            Speaker.presetVoiceID = newOption;
        }
    }
}
