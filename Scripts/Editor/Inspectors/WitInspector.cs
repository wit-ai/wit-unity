/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System.Security.Cryptography.X509Certificates;
using UnityEditor;
using UnityEngine;

namespace com.facebook.witai.Inspectors
{
    [CustomEditor(typeof(Wit))]
    public class WitInspector : Editor
    {
        private string activationMessage;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                var wit = (Wit) target;

                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Activate"))
                {
                    wit.Activate();
                }

                if (GUILayout.Button("Sound Activate"))
                {
                    wit.Activate(Wit.ActivationMode.SoundActivate);
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                activationMessage = GUILayout.TextField(activationMessage);
                if (GUILayout.Button("Send"))
                {
                    wit.Activate(activationMessage);
                }
                GUILayout.EndHorizontal();
            }
        }
    }
}
