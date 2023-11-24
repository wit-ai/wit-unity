/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Collections.Generic;
using Meta.WitAi.Attributes;
using Meta.WitAi.TTS.Interfaces;
using UnityEngine;

namespace Meta.Voice.Samples.TTSLipSync
{
    /// <summary>
    /// A class that toggles on a 'speaking' animator key
    /// based on a TTSSpeaker's IsSpeaking book
    /// </summary>
    public class BodySpeakingAnimation : MonoBehaviour
    {
        /// <summary>
        /// Speaker to watch for adjusting animation
        /// </summary>
        public ISpeaker Speaker => _speaker as ISpeaker;
        [SerializeField, ObjectType(typeof(ISpeaker))]
        private UnityEngine.Object _speaker;

        /// <summary>
        /// Body animator reference
        /// </summary>
        public Animator Animator;

        /// <summary>
        /// The animator id for speaking
        /// </summary>
        [DropDown("GetAnimatorKeys")]
        public string AnimatorSpeakKey = "SPEAKING";

        // The set value for speaking
        private bool _speaking = false;
        private bool _pausing = false;

        // Get speaker & body animator on awake if possible
        protected virtual void Awake()
        {
            if (Speaker == null)
            {
                _speaker = gameObject.GetComponentInChildren(typeof(ISpeaker));
            }
            if (Animator == null)
            {
                Animator = gameObject.GetComponentInChildren<Animator>();
            }
        }

        // Update if possible
        private void Update()
        {
            RefreshPausing();
            RefreshSpeaking();
        }

        // Refresh speaking
        public void RefreshSpeaking()
        {
            // Ensure speaking has changed
            bool shouldSpeak = Speaker != null && Speaker.IsSpeaking;
            if (_speaking == shouldSpeak)
            {
                return;
            }

            // Set speaking
            _speaking = shouldSpeak;

            // Apply to animator if possible
            if (Animator != null)
            {
                Animator.SetBool(AnimatorSpeakKey, _speaking);
            }
        }

        // Refresh pause
        public void RefreshPausing()
        {
            // Ensure speaking has changed
            bool shouldPause = Speaker != null && Speaker.IsPaused;
            if (_pausing == shouldPause)
            {
                return;
            }

            // Set pausing
            _pausing = shouldPause;

            // Apply to animator if possible
            if (Animator != null)
            {
                Animator.speed = _pausing ? 0f : 1f;
            }
        }

        #if UNITY_EDITOR
        private List<string> _animatorKeys = new List<string>();

        /// <summary>
        /// Getter method to be used by editor dropdown menu that returns all animator
        /// parameter keys to allow for easier animator key lookup
        /// </summary>
        public List<string> GetAnimatorKeys()
        {
            if (_animatorKeys == null)
            {
                _animatorKeys = new List<string>();
            }
            if (Animator != null && Animator.parameters != null && _animatorKeys.Count != Animator.parameters.Length)
            {
                foreach (var param in Animator.parameters)
                {
                    _animatorKeys.Add(param.name);
                }
            }
            return _animatorKeys;
        }
        #endif
    }
}
