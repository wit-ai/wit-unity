/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Collections;
using System.Collections.Generic;
using Meta.WitAi;
using Meta.WitAi.Data;
using Meta.WitAi.Interfaces;
using UnityEngine;

public class AudioClipAudioSource : MonoBehaviour, IAudioInputSource
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private List<AudioClip> _audioClips;
    [Tooltip("If true, the associated clips will be played again from the beginning with multiple requests after the clip queue has been exhausted.")]
    [SerializeField] private bool _loopRequests;

    private bool _isRecording;

    private Queue<int> _audioQueue = new Queue<int>();
    private int clipIndex = 0;

    private List<float[]> clipData = new List<float[]>();

    private void Start()
    {
        foreach (var clip in _audioClips)
        {
            AddClipData(clip);
            VLog.D($"Added {clip.name} to queue");
        }
    }

    public event Action OnStartRecording;
    public event Action OnStartRecordingFailed;
    public event Action<int, float[], float> OnSampleReady;
    public event Action OnStopRecording;
    public void StartRecording(int sampleLen)
    {
        if (_isRecording)
        {
            OnStartRecordingFailed?.Invoke();
            return;
        }
        _isRecording = true;
        PlayNextClip();
    }

    private void PlayNextClip()
    {
        if (clipIndex >= _audioClips.Count && _loopRequests)
        {
            clipIndex = 0;
        }
        if (clipIndex < _audioClips.Count)
        {
            VLog.D($"Starting clip {clipIndex}");
            _isRecording = true;
            VLog.D($"Playing {_audioClips[clipIndex].name}");
            _audioSource.PlayOneShot(_audioClips[clipIndex]);
            StartCoroutine(ProcessClip(_audioClips[clipIndex], clipData[clipIndex]));
            OnStartRecording?.Invoke();
        }
        else
        {
            OnStartRecordingFailed?.Invoke();
        }
    }

    private IEnumerator ProcessClip(AudioClip clip, float[] clipData)
    {
        int chunkSize = 0;
        for (int index = 0; index < clipData.Length; index += chunkSize)
        {
            chunkSize = (int) (16000 * Time.deltaTime);
            int len = Math.Min(chunkSize, clipData.Length - index);
            var data = new float[chunkSize];
            Array.Copy(clipData, index, data, 0, len);

            var max = float.MinValue;
            foreach (var f in data)
            {
                max = Mathf.Max(f, max);
            }

            VLog.D($"Sending {index}/{clipData.Length} [{data.Length}] samples with a max volume of {max}");
            OnSampleReady?.Invoke(data.Length, data, max);
            yield return null;
        }

        if (_loopRequests)
        {
            StopRecording();
            PlayNextClip();
        }
        else
        {
            StopRecording();
            clipIndex++;
        }
    }

    public void StopRecording()
    {
        _isRecording = false;
        OnStopRecording?.Invoke();
    }

    public bool IsRecording => _isRecording;

    public AudioEncoding AudioEncoding => new AudioEncoding();
    public bool IsInputAvailable => true;
    public void CheckForInput()
    {

    }

    public bool SetActiveClip(string clipName)
    {
        int index = _audioClips.FindIndex(0, (AudioClip clip) => {
            if (clip.name == clipName)
            {
                return true;
            }

            return false;
        });

        if (index < 0 || index >= _audioClips.Count)
        {
            VLog.D($"Couldn't find clip {clipName}");
            return false;
        }

        clipIndex = index;
        return true;
    }

    public void AddClip(AudioClip clip)
    {
        _audioClips.Add(clip);
        AddClipData(clip);
        VLog.D($"Clip added {clip.name}");
    }

    private void AddClipData(AudioClip clip)
    {
        var samples = new float[clip.samples];
        clip.GetData(samples, 0);
        clipData.Add(samples);
    }
}
