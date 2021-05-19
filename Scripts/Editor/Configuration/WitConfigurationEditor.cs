using System;
using System.Collections.Generic;
using System.Linq;
using com.facebook.witai.data;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WitConfiguration))]
public class WitConfigurationEditor : Editor
{
    private WitConfiguration configuration;

    private Dictionary<string, bool> foldouts = new Dictionary<string, bool>();

    private int appIndex = -1;
    private int selectedToolPanel;

    private readonly string[] toolPanelNames = new []
    {
        "Intents",
        "Entities"
    };

    private const int TOOL_PANEL_INTENTS = 0;
    private const int TOOL_PANEL_ENTITIES = 1;

    private Editor applicationEditor;
    private Vector2 scroll;



    private bool IsTokenValid => !string.IsNullOrEmpty(configuration.clientAccessToken) &&
                                 configuration.clientAccessToken.Length == 32;

    private void OnEnable()
    {
        configuration = target as WitConfiguration;
        configuration.Update();
    }

    public override void OnInspectorGUI()
    {
        configuration = target as WitConfiguration;

        GUILayout.BeginVertical(EditorStyles.helpBox);
        GUILayout.Label("Application Configuration", EditorStyles.boldLabel);

        GUILayout.BeginHorizontal();
        var token = EditorGUILayout.PasswordField("Client Access Token", configuration.clientAccessToken);
        if (token != configuration.clientAccessToken)
        {
            configuration.clientAccessToken = token;

            if (token.Length == 32)
            {
                configuration.Update();
            }

            EditorUtility.SetDirty(configuration);
        }
        if (IsTokenValid)
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(75)))
            {
                configuration.Update();
            }
        }

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        selectedToolPanel = GUILayout.Toolbar(selectedToolPanel, toolPanelNames);
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.ExpandHeight(true));
        switch (selectedToolPanel)
        {
            /*case TOOL_PANEL_APP:
                DrawApplication();
                break;*/
            case TOOL_PANEL_INTENTS:
                DrawIntents();
                break;
            case TOOL_PANEL_ENTITIES:
                DrawEntities();
                break;
        }
        GUILayout.EndScrollView();

        if (GUILayout.Button("Open Wit.ai"))
        {
            /*if (!string.IsNullOrEmpty(configuration.ActiveApplication?.id))
            {
                Application.OpenURL($"https://wit.ai/apps/{configuration.ActiveApplication.id}");
            }
            else
            {*/
                Application.OpenURL("https://wit.ai");
            //}
        }
    }

    private void DrawEntities()
    {
        BeginIndent();
        for (int i = 0; i < configuration.entities.Length; i++)
        {
            var entity = configuration.entities[i];
            if (null != entity && Foldout("e:", entity.name))
            {
                DrawEntity(entity);
            }
        }
        EndIndent();
    }

    private void DrawEntity(WitEntity entity)
    {
        InfoField("ID", entity.id);
        if (entity.roles.Length > 0)
        {
            EditorGUILayout.Popup("Roles", 0, entity.roles);
        }

        if (entity.lookups.Length > 0)
        {
            EditorGUILayout.Popup("Lookups", 0, entity.lookups);
        }
    }

    private void DrawIntents()
    {
        BeginIndent();
        for (int i = 0; i < configuration.intents.Length; i++)
        {
            var intent = configuration.intents[i];
            if (null != intent && Foldout("i:", intent.name))
            {
                DrawIntent(intent);
            }
        }
        EndIndent();
    }

    private void DrawIntent(WitIntent intent)
    {
        InfoField("ID", intent.id);
        if (intent.entities.Length > 0)
        {
            var entityNames = intent.entities.Select(e => e.name).ToArray();
            EditorGUILayout.Popup("Entities", 0, entityNames);
        }
    }

    private void DrawApplication(WitApplication application)
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

    #region UI Components
    private void BeginIndent()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(10);
        GUILayout.BeginVertical();
    }

    private void EndIndent()
    {
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
    }

    private void InfoField(string name, string value)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name, GUILayout.Width(100));
        GUILayout.Label(value, "TextField");
        GUILayout.EndHorizontal();
    }

    private bool Foldout(string keybase, string name)
    {
        string key = keybase + name;
        bool show = false;
        if (!foldouts.TryGetValue(key, out show))
        {
            foldouts[key] = false;
        }

        show = EditorGUILayout.Foldout(show, name, true);
        foldouts[key] = show;
        return show;
    }
    #endregion
}
