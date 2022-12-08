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
using Meta.WitAi.Data;
using Meta.WitAi.Interfaces;
using UnityEngine;

public class AudioClipAudioSource : MonoBehaviour, IAudioInputSource
{
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _audioClips;
    private bool _isRecording;

    private Queue<int> _audioQueue = new Queue<int>();
    private int clipIndex = 0;
    private float[] activeClip;
    private int activeClipIndex;
    private float[] activeClipBuffer = new float[0];

    private List<float[]> clipData = new List<float[]>();

    private void Start()
    {
        foreach (var clip in _audioClips)
        {
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            clipData.Add(samples);
            Debug.Log($"Added {clip.name} to queue");
        }
    }

    private void SendSample(float[] sample)
    {
        var len = Math.Min(_audioQueue.Dequeue(), activeClip.Length - activeClipIndex);

        try
        {
            Debug.Log("Sending length: " + len);
            activeClipBuffer = new float[len];
            Array.Copy(activeClip, activeClipIndex, activeClipBuffer, 0, len);
            activeClipIndex += len;
        }
        catch (ArgumentException e)
        {
            Debug.Log(e);
        }

        var max = float.MinValue;
        foreach (var f in activeClipBuffer)
        {
            max = Mathf.Max(f, max);
        }

        OnSampleReady?.Invoke(activeClipBuffer.Length, activeClipBuffer, max);
    }

    public event Action OnStartRecording;
    public event Action OnStartRecordingFailed;
    public event Action<int, float[], float> OnSampleReady;
    public event Action OnStopRecording;
    public void StartRecording(int sampleLen)
    {
        if (_isRecording) return;
        _isRecording = true;
        if (clipIndex < _audioClips.Length)
        {
            activeClip = clipData[clipIndex];
            activeClipIndex = 0;
            Debug.Log($"Starting clip {clipIndex}");
            _isRecording = true;
            Debug.Log($"Playing {_audioClips[clipIndex].name}");
            _audioSource.PlayOneShot(_audioClips[clipIndex]);
            StartCoroutine(ProcessClip(_audioClips[clipIndex], clipData[clipIndex]));
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

            Debug.Log($"Sending {index}/{clipData.Length} [{data.Length}] samples with a max volume of {max}");
            OnSampleReady?.Invoke(data.Length, data, max);
            yield return null;
        }
        _isRecording = false;
        clipIndex++;
    }

    public void StopRecording()
    {
        _isRecording = false;
    }

    public bool IsRecording => _isRecording;

    public AudioEncoding AudioEncoding => new AudioEncoding();
    public bool IsInputAvailable => true;
    public void CheckForInput()
    {

    }

    public bool SetActiveClip(string clipName)
    {
        int index = Array.FindIndex(_audioClips, (AudioClip clip) => {
            if (clip.name == clipName)
            {
                return true;
            }

            return false;
        });

        if (index < 0 || index >= _audioClips.Length)
        {
            Debug.Log($"Couldn't find clip {clipName}");
            return false;
        }

        activeClipIndex = index;
        activeClip = clipData[index];
        return true;
    }
}
