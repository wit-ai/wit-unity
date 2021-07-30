/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
using System.Runtime.CompilerServices;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

#endif

public class WitAuthUtility
{
    public static bool IsServerTokenValid
    {
        get
        {
            var token = ServerToken;
            return null != token && token.Length == 32;
        }
    }

    private static string serverToken;
    public static string ServerToken
    {
#if UNITY_EDITOR
        get
        {
            if (null == serverToken)
            {
                try
                {
                    serverToken = EditorPrefs.GetString("Wit::ServerToken", "");
                }
                catch (Exception e)
                {
                    // This will happen if we don't prime the server token on the main thread and
                    // we access the server token editorpref value in a request.
                    Debug.LogError(e.Message);
                }
            }

            return serverToken;
        }
        set
        {
            serverToken = value;
            EditorPrefs.SetString("Wit::ServerToken", serverToken);
        }
#else
        get => "";
#endif
    }

#if UNITY_EDITOR
    public static void InitEditorTokens()
    {
        if (null == serverToken)
        {
            serverToken = EditorPrefs.GetString("Wit::ServerToken", "");
        }
    }
#endif
}
