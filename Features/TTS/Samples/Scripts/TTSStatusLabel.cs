/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Text;
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
            _speaker.Events.OnClipLoadBegin.AddListener(OnClipRefresh);
            _speaker.Events.OnClipLoadAbort.AddListener(OnClipRefresh);
            _speaker.Events.OnClipLoadFailed.AddListener(OnClipRefresh);
            _speaker.Events.OnClipLoadSuccess.AddListener(OnClipRefresh);
            _speaker.Events.OnQueuedSpeaking.AddListener(OnClipRefresh);
            _speaker.Events.OnStartSpeaking.AddListener(OnClipRefresh);
            _speaker.Events.OnCancelledSpeaking.AddListener(OnClipRefresh);
            _speaker.Events.OnFinishedSpeaking.AddListener(OnClipRefresh);
        }
        private void OnClipRefresh(TTSSpeaker speaker, string textToSpeak)
        {
            RefreshLabel();
        }
        private void OnDisable()
        {
            _speaker.Events.OnClipLoadBegin.RemoveListener(OnClipRefresh);
            _speaker.Events.OnClipLoadAbort.RemoveListener(OnClipRefresh);
            _speaker.Events.OnClipLoadFailed.RemoveListener(OnClipRefresh);
            _speaker.Events.OnClipLoadSuccess.RemoveListener(OnClipRefresh);
            _speaker.Events.OnQueuedSpeaking.RemoveListener(OnClipRefresh);
            _speaker.Events.OnStartSpeaking.RemoveListener(OnClipRefresh);
            _speaker.Events.OnCancelledSpeaking.RemoveListener(OnClipRefresh);
            _speaker.Events.OnFinishedSpeaking.RemoveListener(OnClipRefresh);
        }

        private void RefreshLabel()
        {
            StringBuilder status = new StringBuilder();

            if (_speaker.IsLoading)
            {
                status.AppendLine($"Loading ({_speaker.LoadingClips.Length})");
            }
            if (_speaker.IsSpeaking)
            {
                status.AppendLine($"Play Queue ({_speaker.PlaybackQueue.Length})");
            }

            _label.text = status.ToString();
        }
    }
}
