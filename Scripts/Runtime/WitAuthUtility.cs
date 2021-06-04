/*
 * Copyright (c) Facebook, Inc. and its affiliates.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class WitAuthUtility
{
    public static bool IsIDETokenValid => Tokens.Length == 3;
    public static string IDEToken
    {
#if UNITY_EDITOR
        get { return EditorPrefs.GetString("Wit::IDEToken", ""); }
        set { EditorPrefs.SetString("Wit::IDEToken", value); }
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

    public static string ServerToken
    {
#if UNITY_EDITOR
        get => Tokens.Length >= 2 ? Tokens[1] : null;
#else
        get => "";
#endif
    }

    public static string ClientToken
    {
#if UNITY_EDITOR
        get => Tokens.Length >= 3 ? Tokens[2] : null;
#else
        get => "";
#endif
    }
}
