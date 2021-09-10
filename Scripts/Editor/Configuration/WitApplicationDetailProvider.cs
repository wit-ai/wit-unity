/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */
using UnityEditor;
using UnityEngine;
using com.facebook.witai;
using com.facebook.witai.data;
using com.facebook.witai.utility;
using com.facebook.witai.interfaces;

public class WitApplicationDetailProvider : IApplicationDetailProvider
{
    public void DrawApplication(WitApplication application)
    {
        if (string.IsNullOrEmpty(application.name))
        {
            GUILayout.Label("Loading...");
        }
        else
        {
            if (!string.IsNullOrEmpty(WitAuthUtility.AppServerToken))
            {
                InfoField("Name", application.name);
                InfoField("ID", application.id);
                InfoField("Language", application.lang);
                InfoField("Created", application.createdAt);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Private", GUILayout.Width(100));
                GUILayout.Toggle(application.isPrivate, "");
                GUILayout.EndHorizontal();
            }
        }
    }

    private void InfoField(string name, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, GUILayout.Width(100));
        GUILayout.Label(value, "TextField");
        GUILayout.EndHorizontal();
    }
}