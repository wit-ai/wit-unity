/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
using Meta.WitAi.TTS.Data;
using UnityEngine;
using UnityEngine.UI;
using Meta.WitAi.TTS.Utilities;

namespace Meta.WitAi.TTS.Samples
{
    public class TTSStatusLabel : MonoBehaviour
    {
        [SerializeField] private TTSSpeaker _speaker;
        [SerializeField] private Text _label;

        private void OnEnable()
        {
            RefreshLabel();
            _speaker.Events.OnLoadBegin.AddListener(OnClipRefresh);
            _speaker.Events.OnLoadAbort.AddListener(OnClipRefresh);
            _speaker.Events.OnLoadFailed.AddListener(OnClipRefresh);
            _speaker.Events.OnLoadSuccess.AddListener(OnClipRefresh);
            _speaker.Events.OnPlaybackReady.AddListener(OnClipRefresh);
            _speaker.Events.OnPlaybackStart.AddListener(OnClipRefresh);
            _speaker.Events.OnPlaybackCancelled.AddListener(OnClipRefresh);
            _speaker.Events.OnPlaybackComplete.AddListener(OnClipRefresh);
        }
        private void OnClipRefresh(TTSSpeaker speaker, TTSClipData clipData, string error)
        {
            RefreshLabel();
        }
        private void OnClipRefresh(TTSSpeaker speaker, TTSClipData clipData)
        {
            RefreshLabel();
        }
        private void OnDisable()
        {
            _speaker.Events.OnLoadBegin.RemoveListener(OnClipRefresh);
            _speaker.Events.OnLoadAbort.RemoveListener(OnClipRefresh);
            _speaker.Events.OnLoadFailed.RemoveListener(OnClipRefresh);
            _speaker.Events.OnLoadSuccess.RemoveListener(OnClipRefresh);
            _speaker.Events.OnPlaybackReady.RemoveListener(OnClipRefresh);
            _speaker.Events.OnPlaybackStart.RemoveListener(OnClipRefresh);
            _speaker.Events.OnPlaybackCancelled.RemoveListener(OnClipRefresh);
            _speaker.Events.OnPlaybackComplete.RemoveListener(OnClipRefresh);
        }

        private void RefreshLabel()
        {
            StringBuilder status = new StringBuilder();
            if (_speaker.SpeakingClip != null)
            {
                status.AppendLine($"Speaking: {_speaker.IsSpeaking}");
            }
            int index = 0;
            foreach (var clip in _speaker.QueuedClips)
            {
                status.Insert(0, $"Queue[{index}]: {clip.loadState.ToString()}\n");
                index++;
            }
            if (status.Length > 0)
            {
                status.Remove(status.Length - 1, 1);
            }
            _label.text = status.ToString();
        }
    }
}
