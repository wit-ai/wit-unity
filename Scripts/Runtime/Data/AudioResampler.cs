/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using Meta.Voice.Logging;
using Meta.WitAi.Interfaces;
using UnityEngine;

namespace Meta.WitAi.Data
{
    /// <summary>
    /// A script that handles upsampling or downsampling audio input.  It also determines samplerate if needed.
    /// </summary>
    [LogCategory(LogCategory.Audio, LogCategory.Input)]
    public class AudioResampler
    {
        /// <summary>
        /// Event callback for sample enqueueing
        /// </summary>
        public event Action<float> OnResampled;
        /// <summary>
        /// Event callback for sample individual byte enqueueing
        /// </summary>
        public event Action<byte> OnByteResampled;
        /// <summary>
        /// Event callback for min/max level change
        /// </summary>
        public event Action<float, float> OnLevelChanged;

        // Used to log sample rate calculation
        private static IVLogger _log { get; } = LoggerRegistry.Instance.GetLogger(LogCategory.Input);

        /// <summary>
        /// Resamples input samples into the desired output encoding
        /// </summary>
        /// <param name="fromEncoding">Source encoding of incoming audio data</param>
        /// <param name="fromMinLevel">Min audio threshold expected, used to normalize level</param>
        /// <param name="fromMaxLevel">Max audio threshold expected, used to normalize level</param>
        /// <param name="toEncoding">Desired output encoding</param>
        /// <param name="samples">Incoming data samples</param>
        /// <param name="offset">Offset of the sample array</param>
        /// <param name="length">Length of sample array to be used</param>
        /// <param name="enqueueBytes">Whether we need to enqueue each byte</param>
        /// <param name="variableSampleRate">Used for scripts with custom sample rate</param>
        /// <returns>Returns the min/max of the sample in Vector2</returns>
        public Vector2 Resample(AudioEncoding fromEncoding, float fromMinLevel, float fromMaxLevel,
            AudioEncoding toEncoding, float[] samples, int offset, int length,
            bool enqueueBytes, IAudioVariableSampleRate variableSampleRate = null)
        {
            // Attempt to calculate sample rate if not determined
            if (fromEncoding.samplerate <= 0
                || (variableSampleRate != null && variableSampleRate.NeedsSampleRateCalculation))
            {
                CalculateSampleRate(fromEncoding, length, variableSampleRate?.SkipInitialSamplesInMs ?? 10);
                if (fromEncoding.samplerate <= 0)
                {
                    return Vector2.zero;
                }
            }

            // Get input encoding
            int fromSampleRate = fromEncoding.samplerate;
            int fromChannels = fromEncoding.numChannels;
            bool fromSigned = string.Equals(fromEncoding.encoding, Data.AudioEncoding.ENCODING_SIGNED);

            // Get output encoding
            int toSampleRate = toEncoding.samplerate;
            int bytesPerSample = Mathf.CeilToInt(toEncoding.bits / 8f);
            GetEncodingMinMax(toEncoding.bits, string.Equals(toEncoding.encoding, AudioEncoding.ENCODING_SIGNED),
                out long encodingMin, out long encodingMax);
            long encodingDif = encodingMax - encodingMin;

            // Determine resize factor & total samples
            float resizeFactor = fromSampleRate == toSampleRate ? 1f : (float)fromSampleRate / toSampleRate;
            resizeFactor *= fromChannels; // Skip all additional channels
            int totalSamples = (int)(length / resizeFactor);

            // Resample
            Vector2 levelMinMax = new Vector2(1f, 0f);
            for (int i = 0; i < totalSamples; i++)
            {
                // Get sample
                var micIndex = offset +  (int)(i * resizeFactor);
                var sample = samples[micIndex];

                // If signed from source (-1 to 1), convert to unsigned (0 to 1)
                if (fromSigned)
                {
                    sample = sample / 2f + 0.5f;
                }

                // Get min/max sample size
                levelMinMax.x = Mathf.Min(levelMinMax.x, sample);
                levelMinMax.y = Mathf.Max(levelMinMax.y, sample);

                // Resampled callback
                OnResampled?.Invoke(sample);

                // If enqueued bytes are desired, enqueue each byte
                if (enqueueBytes)
                {
                    var data = (long)(encodingMin + sample * encodingDif);
                    for (int b = 0; b < bytesPerSample; b++)
                    {
                        var outByte = (byte)(data >> (b * 8));
                        OnByteResampled?.Invoke(outByte);
                    }
                }
            }

            // Scale based on min/max audio levels if possible
            if ((!fromMinLevel.Equals(0f) || !fromMaxLevel.Equals(1f)) && fromMaxLevel > fromMinLevel)
            {
                levelMinMax.x = (levelMinMax.x - fromMinLevel) / (fromMaxLevel - fromMinLevel);
                levelMinMax.y = (levelMinMax.y - fromMinLevel) / (fromMaxLevel - fromMinLevel);
            }

            // Clamp result 0 to 1
            levelMinMax.x = Mathf.Clamp01(levelMinMax.x);
            levelMinMax.y = Mathf.Clamp01(levelMinMax.y);
            OnLevelChanged?.Invoke(levelMinMax.x, levelMinMax.y);
            return levelMinMax;
        }

        /// <summary>
        /// Get encoding min/max for resampling
        /// </summary>
        private void GetEncodingMinMax(int bits, bool signed, out long encodingMin, out long encodingMax)
        {
            switch (bits)
            {
                // Always unsigned
                case AudioEncoding.BITS_BYTE:
                    encodingMin = byte.MinValue;
                    encodingMax = byte.MaxValue;
                    break;
                // Always signed
                case AudioEncoding.BITS_LONG:
                    encodingMin = long.MinValue;
                    encodingMax = long.MaxValue;
                    break;
                // Signed/Unsigned
                case AudioEncoding.BITS_INT:
                    encodingMin = signed ? int.MinValue : uint.MinValue;
                    encodingMax = signed ? int.MaxValue : uint.MaxValue;
                    break;
                // Signed/Unsigned
                case AudioEncoding.BITS_SHORT:
                default:
                    encodingMin = signed ? short.MinValue : ushort.MinValue;
                    encodingMax = signed ? short.MaxValue : ushort.MaxValue;
                    break;
            }
        }

        #region Sample Rate Determination
        // Last sample time tracked in unix ms
        private long _lastSampleTime;
        // First sample time of current calculation in unix ms
        private long _startSampleTime;
        // The currently measured sample total
        private long _measureSampleTotal;
        // Whether currently skipping initial recovery
        private bool _timeoutThrottle;
        // The current measurement index
        private int _measuredSampleRateCount;
        // The various measured sample rates
        private readonly double[] _measuredSampleRates = new double[MEASURE_AVERAGE_COUNT];

        // Timeout if no samples after specified interval
        private const int RESTART_INTERVAL_MS = 50;
        // Perform calculation after specified interval
        private const int MEASURE_INTERVAL_MS = 250;
        // Don't set until measured this many times
        private const int MEASURE_USE_COUNT = 2;
        // Total measurements to average out (3 seconds)
        private const int MEASURE_AVERAGE_COUNT = 12;
        // Sample rate options
        private static readonly int[] ALLOWED_SAMPLE_RATES = new []
        {
            8000,
            11025,
            16000,
            22050,
            32000,
            44100,
            48000,
            88200,
            96000,
            176400,
            192000
        };

        /// <summary>
        /// Calculates sample rate using the current length
        /// </summary>
        private void CalculateSampleRate(AudioEncoding fromEncoding, int sampleLength, int skipInitialSamplesInMs)
        {
            // Ignore invalid sample length
            if (sampleLength <= 0)
            {
                return;
            }

            // Check if calculation restart is needed
            var newSampleTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var deltaSampleTime = newSampleTime - _lastSampleTime;
            _lastSampleTime = newSampleTime;
            if (deltaSampleTime >= RESTART_INTERVAL_MS || _startSampleTime == 0)
            {
                _startSampleTime = newSampleTime;
                _measureSampleTotal = 0;
                _timeoutThrottle = true;
                return;
            }

            // Don't count until after timeout skip
            var elapsedMs = newSampleTime - _startSampleTime;
            if (elapsedMs < skipInitialSamplesInMs && _timeoutThrottle)
            {
                return;
            }

            // Append sample length
            int channels = fromEncoding.numChannels;
            _measureSampleTotal += Mathf.FloorToInt((float)sampleLength / channels);

            // Ignore until ready to calculate
            if (elapsedMs < MEASURE_INTERVAL_MS)
            {
                return;
            }

            // Perform calculation
            var elapsedSeconds = elapsedMs / 1000d;
            var samplesPerSecond = _measureSampleTotal / elapsedSeconds;

            // Add to array and average out
            var index = _measuredSampleRateCount % MEASURE_AVERAGE_COUNT;
            _measuredSampleRates[index] = samplesPerSecond;
            _measuredSampleRateCount++;
            _timeoutThrottle = false;
            if (_measuredSampleRateCount == MEASURE_AVERAGE_COUNT * 2) _measuredSampleRateCount -= MEASURE_AVERAGE_COUNT;
            var averageSampleRate = GetAverageSampleRate(_measuredSampleRates, _measuredSampleRateCount);

            // Determine closest sample rate using averaged value
            var closestSampleRate = GetClosestSampleRate(averageSampleRate);
            if (_measuredSampleRateCount >= MEASURE_USE_COUNT && !fromEncoding.samplerate.Equals(closestSampleRate))
            {
                fromEncoding.samplerate = closestSampleRate;
                _log.Info("Input SampleRate Set: {0}\nElapsed: {1:0.000} seconds\nAverage Samples per Second: {2}",
                    closestSampleRate, elapsedSeconds, averageSampleRate);
            }

            // Restart calculation
            _startSampleTime = newSampleTime;
            _measureSampleTotal = 0;
        }

        /// <summary>
        /// Return average sample rate
        /// </summary>
        private static double GetAverageSampleRate(double[] sampleRates, int sampleRateCount)
        {
            // Ignore if invalid total
            var count = Mathf.Min(sampleRateCount, sampleRates.Length);
            if (count <= 0)
            {
                return 0d;
            }
            // Iterate each sample
            var result = 0d;
            for (int i = 0; i < count; i++)
            {
                result += sampleRates[i];
            }
            // Return average
            return result / count;
        }

        /// <summary>
        /// Obtains the closest sample rate using the samples per second
        /// </summary>
        private static int GetClosestSampleRate(double samplesPerSecond)
        {
            // Iterate sample rates
            var result = 0;
            var diff = int.MaxValue;
            var samplesPerSecondInt = (int)Math.Round(samplesPerSecond);
            for (int i = 0; i < ALLOWED_SAMPLE_RATES.Length; i++)
            {
                // Determine difference between sample rates
                var sampleRate = ALLOWED_SAMPLE_RATES[i];
                var check = Mathf.Abs(sampleRate - samplesPerSecondInt);
                // Closer, replace
                if (check < diff)
                {
                    result = sampleRate;
                    diff = check;
                }
                // More, return previous
                else
                {
                    return result;
                }
            }
            // Return result
            return result;
        }
        #endregion Sample Rate Determination
    }
}
