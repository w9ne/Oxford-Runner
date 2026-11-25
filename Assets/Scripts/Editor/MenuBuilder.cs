using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Build Main Menu
/// Creates Scenes/MainMenu.unity (camera + MainMenu component), puts it first in the build list
/// (MainMenu, Tutorial), and adds the pause menu to the Player prefab. Safe to re-run.
/// </summary>
public static class MenuBuilder
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private static readonly string[] BuildOrder =
    {
        MenuScenePath, "Assets/Scenes/Tutorial.unity",
    };

    [MenuItem("Oxford Runner/Build Main Menu")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGO.tag = "MainCamera";
        var cam = camGO.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.12f);

        new GameObject("MainMenu", typeof(MainMenu));

        EditorSceneManager.SaveScene(scene, MenuScenePath);

        // Build list: menu first, then the rest in play order, keeping any other scenes after them.
        var scenes = BuildOrder.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        foreach (var s in EditorBuildSettings.scenes)
            if (!BuildOrder.Contains(s.path)) scenes.Add(s);
        EditorBuildSettings.scenes = scenes.ToArray();

        // Pause menu on the player, so every level has it.
        var player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        try
        {
            if (player.GetComponent<PauseMenu>() == null) player.AddComponent<PauseMenu>();
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        Debug.Log("[MenuBuilder] Main menu built and set as the first scene; pause menu added to the Player.");
    }
}
