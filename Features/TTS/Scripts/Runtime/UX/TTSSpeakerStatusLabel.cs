/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using Meta.WitAi.TTS.Data;
using Meta.WitAi.TTS.Utilities;

namespace Meta.WitAi.TTS.UX
{
    /// <summary>
    /// A simple script for providing clip information to a Text label
    /// </summary>
    public class TTSSpeakerStatusLabel : TTSSpeakerObserver
    {
        // The label to be used for the speaker's status
        [SerializeField] private Text _label;

        // Fields for
        private bool _needsRefresh = true;
        private Coroutine _refreshUpdater;

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshLabel();
            _refreshUpdater = StartCoroutine(RefreshUpdater());
        }

        protected override void OnDisable()
        {
            if (_refreshUpdater != null)
            {
                StopCoroutine(_refreshUpdater);
                _refreshUpdater = null;
            }
        }

        protected override void OnLoadBegin(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }
        protected override void OnLoadAbort(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }
        protected override void OnLoadFailed(TTSSpeaker speaker, TTSClipData clipData, string error)
        {
            _needsRefresh = true;
        }
        protected override void OnLoadSuccess(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }
        protected override void OnPlaybackReady(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }
        protected override void OnPlaybackStart(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }
        protected override void OnPlaybackCancelled(TTSSpeaker speaker, TTSClipData clipData, string reason)
        {
            _needsRefresh = true;
        }
        protected override void OnPlaybackComplete(TTSSpeaker speaker, TTSClipData clipData)
        {
            _needsRefresh = true;
        }

        private IEnumerator RefreshUpdater()
        {
            while (true)
            {
                if (_needsRefresh)
                {
                    RefreshLabel();
                }
                yield return new WaitForEndOfFrame();
            }
        }

        private void RefreshLabel()
        {
            _needsRefresh = false;
            StringBuilder status = new StringBuilder();
            if (Speaker.IsSpeaking)
            {
                AppendClipText(status, Speaker.SpeakingClip, "Speaking");
            }
            int count = 1;
            foreach (var clip in Speaker.QueuedClips)
            {
                AppendClipText(status, clip, $"Queue[{count}]");
                count++;
            }
            _label.text = status.ToString();
            _label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, _label.preferredHeight);
        }

        private void AppendClipText(StringBuilder status, TTSClipData clipData, string clipKey)
        {
            status.AppendLine(clipKey);
            status.AppendLine($"\tText: '{clipData.textToSpeak}'");
            status.AppendLine($"\tVoice: {(clipData.voiceSettings == null ? "" : clipData.voiceSettings.SettingsId)}");
            status.AppendLine($"\tType: {clipData.audioType}");
            status.AppendLine($"\tStatus: {clipData.loadState}");
            if (clipData.loadState == TTSClipLoadState.Loaded)
            {
                status.AppendLine($"\tLoad Time: {clipData.loadDuration:0.000} seconds");
            }
            status.Append("\n");
        }
    }
}
