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
using System.Threading.Tasks;
using Meta.Voice.Logging;
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

    /// <inheritdoc/>
    public IVLogger _log { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Audio);

    #region Muting
    /// <inheritdoc />
    public virtual bool IsMuted { get; private set; } = false;

    /// <inheritdoc />
    public event Action OnMicMuted;

    /// <inheritdoc />
    public event Action OnMicUnmuted;

    protected virtual void SetMuted(bool muted)
    {
        if (IsMuted != muted)
        {
            IsMuted = muted;
            if(IsMuted) OnMicMuted?.Invoke();
            else OnMicUnmuted?.Invoke();
        }
    }
    #endregion

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
            OnStartRecording?.Invoke();
            _ = TransmitAudio(clipData[clipIndex]);
        }
        else
        {
            OnStartRecordingFailed?.Invoke();
        }
    }

    /// <summary>
    /// Transmit audio clip data as playback occurs to simulate microphone recording
    /// </summary>
    private float[] _buffer;
    private const float _samplesPerFrame = 1f / 100f;
    private async Task TransmitAudio(float[] samples)
    {
        // Generate a single buffer
        int index = 0;
        if (_buffer == null)
        {
            int bufferSize = Mathf.CeilToInt(AudioEncoding.samplerate * _samplesPerFrame);
            _buffer = new float[bufferSize];
        }
        while (index < samples.Length)
        {
            // Clamp
            int len = Math.Min(_buffer.Length, samples.Length - index);
            // Copy and return
            Array.Copy(samples, index, _buffer, 0, len);
            OnSampleReady?.Invoke(len, _buffer, float.MinValue);
            index += len;
            // Wait a frame
            await Task.Yield();
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

    /// <summary>
    /// The audio encoding of the clips being transmitted
    /// </summary>
    public AudioEncoding AudioEncoding => _audioEncoding;
    [SerializeField] private AudioEncoding _audioEncoding = new AudioEncoding();

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
        var clipSamples = new float[clip.samples];
        clip.GetData(clipSamples, 0);
        var transmitSamples = QuickResample(clipSamples,
            clip.channels, clip.frequency,
            AudioEncoding.numChannels, AudioEncoding.samplerate);
        clipData.Add(transmitSamples);
    }

    /// <summary>
    /// Resamples the audio clip to match the current AudioEncoding since AudioBuffer resamples based on the AudioEncoding.
    /// This allows for transmission of clips with different sample rates and channel counts.
    /// </summary>
    public static float[] QuickResample(float[] oldSamples, int oldChannels, int oldSampleRate, int newChannels, int newSampleRate)
    {
        if (oldSampleRate == newSampleRate
            && oldChannels == newChannels)
        {
            return oldSamples;
        }
        float resizeFactor = (float)oldSampleRate / newSampleRate;
        resizeFactor *= (float)oldChannels / newChannels;
        int totalSamples = (int)(oldSamples.Length / resizeFactor);
        float[] newSamples = new float[totalSamples];
        for (int i = 0; i < totalSamples; i++)
        {
            var index = (int)(i * resizeFactor);
            newSamples[i] = oldSamples[index];
        }
        return newSamples;
    }
}
