// This code is licensed under the MIT License.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// Source: https://github.com/adrenak/unimic/blob/master/Assets/UniMic/Runtime/Mic.cs

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace com.facebook.witai.lib
{
    [RequireComponent(typeof(AudioSource))]
    public class Mic : MonoBehaviour
    {
        // ================================================

        #region MEMBERS

        // ================================================
        /// <summary>
        /// Whether the microphone is running
        /// </summary>
        public bool IsRecording { get; private set; }

        /// <summary>
        /// The frequency at which the mic is operating
        /// </summary>
        public int Frequency { get; private set; }

        /// <summary>
        /// Last populated audio sample
        /// </summary>
        public float[] Sample { get; private set; }

        /// <summary>
        /// Sample duration/length in milliseconds
        /// </summary>
        public int SampleDurationMS { get; private set; }

        /// <summary>
        /// The length of the sample float array
        /// </summary>
        public int SampleLength
        {
            get { return Frequency * SampleDurationMS / 1000; }
        }

        /// <summary>
        /// The AudioClip currently being streamed in the Mic
        /// </summary>
        public AudioClip AudioClip { get; private set; }

        /// <summary>
        /// List of all the available Mic devices
        /// </summary>
        public List<string> Devices { get; private set; }

        /// <summary>
        /// Index of the current Mic device in m_Devices
        /// </summary>
        public int CurrentDeviceIndex { get; private set; } = -1;

        /// <summary>
        /// Gets the name of the Mic device currently in use
        /// </summary>
        public string CurrentDeviceName
        {
            get
            {
                if (CurrentDeviceIndex < 0 || CurrentDeviceIndex >= Microphone.devices.Length)
                    return string.Empty;
                return Devices[CurrentDeviceIndex];
            }
        }

        int m_SampleCount = 0;

        #endregion

        // ================================================

        #region EVENTS

        // ================================================
        /// <summary>
        /// Invoked when the instance starts Recording.
        /// </summary>
        public event Action OnStartRecording;

        /// <summary>
        /// Invoked when an AudioClip couldn't be created to start recording.
        /// </summary>
        public event Action OnStartRecordingFailed;

        /// <summary>
        /// Invoked everytime an audio frame is collected. Includes the frame.
        /// </summary>
        public event Action<int, float[], float> OnSampleReady;

        /// <summary>
        /// Invoked when the instance stop Recording.
        /// </summary>
        public event Action OnStopRecording;

        #endregion

        // ================================================

        #region METHODS

        // ================================================

        static Mic m_Instance;

        public static Mic Instance
        {
            get
            {
                if (m_Instance == null)
                    m_Instance = GameObject.FindObjectOfType<Mic>();
                if (m_Instance == null)
                {
                    m_Instance = new GameObject("UniMic.Mic").AddComponent<Mic>();
                    DontDestroyOnLoad(m_Instance.gameObject);
                }

                return m_Instance;
            }
        }

        public static Mic Instantiate()
        {
            return Instance;
        }

        void Awake()
        {
            UpdateDevices();
            CurrentDeviceIndex = 0;
        }

        public void UpdateDevices()
        {
            Devices = new List<string>();
            foreach (var device in Microphone.devices)
                Devices.Add(device);
        }

        /// <summary>
        /// Changes to a Mic device for Recording
        /// </summary>
        /// <param name="index">The index of the Mic device. Refer to <see cref="Devices"/></param>
        public void ChangeDevice(int index)
        {
            Microphone.End(CurrentDeviceName);
            CurrentDeviceIndex = index;
            StartRecording(Frequency, SampleDurationMS);
        }

        /// <summary>
        /// Starts to stream the input of the current Mic device
        /// </summary>
        public void StartRecording(int frequency = 16000, int sampleLen = 10)
        {
            StopRecording();
            IsRecording = true;

            Frequency = frequency;
            SampleDurationMS = sampleLen;

            AudioClip = Microphone.Start(CurrentDeviceName, true, 1, Frequency);
            Sample = new float[Frequency / 1000 * SampleDurationMS * AudioClip.channels];

            if (AudioClip)
            {
                StartCoroutine(ReadRawAudio());

                Debug.Log("Started recording with " + CurrentDeviceName);
                if (OnStartRecording != null)
                    OnStartRecording.Invoke();
            }
            else
            {
                OnStartRecordingFailed.Invoke();
            }
        }

        /// <summary>
        /// Ends the Mic stream.
        /// </summary>
        public void StopRecording()
        {
            if (!Microphone.IsRecording(CurrentDeviceName)) return;

            IsRecording = false;

            Microphone.End(CurrentDeviceName);
            Destroy(AudioClip);
            AudioClip = null;

            StopCoroutine(ReadRawAudio());

            Debug.Log("Stopped recording with " + CurrentDeviceName);
            if (OnStopRecording != null)
                OnStopRecording.Invoke();
        }

        IEnumerator ReadRawAudio()
        {
            int loops = 0;
            int readAbsPos = 0;
            int prevPos = 0;
            float[] temp = new float[Sample.Length];

            while (AudioClip != null && Microphone.IsRecording(CurrentDeviceName))
            {
                bool isNewDataAvailable = true;

                while (isNewDataAvailable && AudioClip)
                {
                    int currPos = Microphone.GetPosition(CurrentDeviceName);
                    if (currPos < prevPos)
                        loops++;
                    prevPos = currPos;

                    var currAbsPos = loops * AudioClip.samples + currPos;
                    var nextReadAbsPos = readAbsPos + temp.Length;
                    float levelMax = 0;

                    if (nextReadAbsPos < currAbsPos)
                    {
                        AudioClip.GetData(temp, readAbsPos % AudioClip.samples);

                        for (int i = 0; i < temp.Length; i++)
                        {
                            float wavePeak = temp[i] * temp[i];
                            if (levelMax < wavePeak)
                            {
                                levelMax = wavePeak;
                            }
                        }

                        Sample = temp;
                        m_SampleCount++;
                        OnSampleReady?.Invoke(m_SampleCount, Sample, levelMax);

                        readAbsPos = nextReadAbsPos;
                        isNewDataAvailable = true;
                    }
                    else
                        isNewDataAvailable = false;
                }

                yield return null;
            }
        }

        #endregion
    }
}
