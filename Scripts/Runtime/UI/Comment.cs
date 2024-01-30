/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * This source code is licensed under the license found in the
 * LICENSE file in the root directory of this source tree.
 */

using UnityEditor;
using UnityEngine;

#nullable disable

namespace Meta.WitAi
{
    public class Comment : MonoBehaviour
    {
        [SerializeField] internal string title;
        [TextArea] 
        [SerializeField] internal string comment;
        [SerializeField] internal bool lockComment;
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(Comment))]
    public class CommentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var comment = (Comment)target;
            if (comment.lockComment)
            {
                if (!string.IsNullOrEmpty(comment.title))
                {
                    GUILayout.Label(comment.title, EditorStyles.boldLabel);
                }
                GUILayout.BeginVertical(EditorStyles.helpBox);
                GUILayout.TextArea(comment.comment);
                GUILayout.EndVertical();
            }
            else
            {
                GUIStyle myTextAreaStyle = new GUIStyle(EditorStyles.textArea);
                myTextAreaStyle.wordWrap = true;
                comment.title = GUILayout.TextField("Title", comment.title);
                comment.comment = EditorGUILayout.TextArea(comment.comment, myTextAreaStyle, GUILayout.MinHeight(300));
                comment.lockComment = EditorGUILayout.Toggle("Locked", comment.lockComment);
            }
        }
    }
    #endif
}
