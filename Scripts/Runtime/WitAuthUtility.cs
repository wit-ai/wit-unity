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
    private static string ideToken;
    public static string IDEToken
    {
#if UNITY_EDITOR
        get
        {
            if (null == ideToken)
            {
                try
                {
                    ideToken = EditorPrefs.GetString("Wit::IDEToken", "");
                }
                catch (Exception e)
                {
                    // This will happen if we don't prime the ide token on the main thread and
                    // we access the server token editorpref value in a request.
                    Debug.LogError(e.Message);
                }
            }

            return ideToken;
        }
        set
        {
            ideToken = value;
            EditorPrefs.SetString("Wit::IDEToken", ideToken);
        }
#else
            get => "";
#endif
    }

    private static string[] Tokens =>
        IDEToken.Split(new string[] {"::"}, StringSplitOptions.RemoveEmptyEntries);

    public static string AppId
    {
#if UNITY_EDITOR
        get => Tokens.Length >= 1 ? Tokens[0] : null;
#else
        get => "";
#endif
    }

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

            if (string.IsNullOrEmpty(serverToken) && Tokens.Length >= 2)
            {
                serverToken = Tokens[1];
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

        if (null == ideToken)
        {
            ideToken = EditorPrefs.GetString("Wit::IDEToken", "");
        }
    }
#endif
}
