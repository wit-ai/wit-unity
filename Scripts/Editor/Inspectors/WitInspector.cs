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
        private Wit wit;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            if (Application.isPlaying)
            {
                wit = (Wit) target;

                if (wit.Active)
                {
                    if (GUILayout.Button("Deactivate"))
                    {
                        wit.Deactivate();
                    }

                    if (wit.MicActive)
                    {
                        GUILayout.Label("Listening...");
                    }
                    else
                    {
                        GUILayout.Label("Processing...");
                    }
                }
                else
                {
                    if (GUILayout.Button("Activate"))
                    {
                        wit.Activate();
                        EditorApplication.update += UpdateWhileActive;
                    }

                    GUILayout.BeginHorizontal();
                    activationMessage = GUILayout.TextField(activationMessage);
                    if (GUILayout.Button("Send", GUILayout.Width(50)))
                    {
                        wit.Activate(activationMessage);
                        EditorApplication.update += UpdateWhileActive;
                    }

                    GUILayout.EndHorizontal();
                }
            }
        }

        private void UpdateWhileActive()
        {
            Repaint();
            if (!wit.Active)
            {
                EditorApplication.update -= UpdateWhileActive;
            }
        }
    }
}
