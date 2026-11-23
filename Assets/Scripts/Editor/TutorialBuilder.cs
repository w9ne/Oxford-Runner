using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Build Tutorial Scene
/// Generates Assets/Scenes/Tutorial.unity: a linear rooftop course over a void that teaches one
/// movement skill per station, ending with a spell-casting range and an exit portal to the arena.
/// Each station is a trigger (prompt + checkpoint); falling into the void respawns at the last one.
///
/// The course runs along +Z. Gap sizes are chosen so each station needs the skill it teaches:
///   jump 4 m | double-jump ledge 2.2 m | dash gap 10 m | slide tunnel | slide-jump gap 15 m |
///   wall-run gap 14 m | wall-jump corridor (+3 m climb) | 5 targets behind a gate |
///   parry a sentinel's bolt back into it | portal
/// </summary>
public static class TutorialBuilder
{
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string ArenaScenePath = "Assets/Scenes/MovementTest.unity";
    private const float Width = 8f;             // standard platform width (X)
    private const float PillarBottom = -8f;     // platforms are pillars down to here
    private const float KillHeight = -12f;

    private struct Station
    {
        public string title, text;
        public float zStart, zEnd;              // trigger span along Z
        public float floorY;                    // top of the floor the player stands on in this zone
        public TutorialGate gateRef;            // optional gate whose progress the prompt shows
    }

    [MenuItem("Oxford Runner/Build Tutorial Scene")]
    public static void Build()
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("Build Tutorial Scene",
                $"Missing {PlayerPrefabPath}.\nOpen MovementTest and run 'Oxford Runner/Create Player Prefab' first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null) Object.DestroyImmediate(defaultCam);   // the player brings its own camera

        var course = Graybox.Group(null, "TutorialCourse");
        var stations = new List<Station>();

        // ---- 1. Move -------------------------------------------------------------------------
        Platform(course, "P1_Start", -6f, 10f, 0f);
        stations.Add(new Station { title = "Move", zStart = -6f, zEnd = 6f, floorY = 0f,
            text = "Use W A S D to move and the mouse to look around.\nRun to the end of the platform." });

        // ---- 2. Jump (4 m gap) ---------------------------------------------------------------
        Platform(course, "P2_Jump", 14f, 24f, 0f);
        stations.Add(new Station { title = "Jump", zStart = 6f, zEnd = 10f, floorY = 0f,
            text = "Press SPACE to jump.\nRun and jump across the gap." });

        // ---- 3. Double jump (2.2 m ledge) ----------------------------------------------------
        Platform(course, "P3_Ledge", 24f, 32f, 2.2f);
        stations.Add(new Station { title = "Double Jump", zStart = 17f, zEnd = 24f, floorY = 0f,
            text = "Press SPACE again while in the air to double jump.\nClimb onto the ledge." });

        // ---- 4. Dash (10 m gap) --------------------------------------------------------------
        stations.Add(new Station { title = "Dash", zStart = 24f, zEnd = 32f, floorY = 2.2f,
            text = "Press LEFT SHIFT to dash the way you're moving.\nJump, then dash in mid-air to cross the gap." });

        // ---- 5. Slide (tunnel) ---------------------------------------------------------------
        Platform(course, "P4_Slide", 42f, 66f, 2.2f);
        Graybox.Box(course, "Tunnel_Roof", new Vector3(0f, 2.2f + 1.3f + 0.25f, 55f), new Vector3(Width, 0.5f, 10f), Graybox.Wall);
        Graybox.Box(course, "Tunnel_Face", new Vector3(0f, 4f + 2.5f, 50.25f), new Vector3(Width, 5f, 0.5f), Graybox.Wall);
        stations.Add(new Station { title = "Slide", zStart = 42f, zEnd = 50f, floorY = 2.2f,
            text = "Hold LEFT CTRL while running to slide.\nSlide under the low ceiling." });

        // ---- 6. Slide jump (15 m gap) --------------------------------------------------------
        Platform(course, "P5_SlideJumpLanding", 81f, 89f, 2.2f);
        stations.Add(new Station { title = "Slide Jump", zStart = 60f, zEnd = 66f, floorY = 2.2f,
            text = "While sliding, press SPACE to slide-jump.\nIt launches you much further than a normal jump." });

        // ---- 7. Wall run (14 m gap, wall on the right) ---------------------------------------
        Graybox.Box(course, "WallRun_Wall", new Vector3(Width * 0.5f + 0.75f, 2f, 96f), new Vector3(0.5f, 12f, 18f), Graybox.WallRun, wallRun: true);
        Platform(course, "P6_WallRunLanding", 103f, 111f, 1f);
        stations.Add(new Station { title = "Wall Run", zStart = 81f, zEnd = 89f, floorY = 2.2f,
            text = "Jump at the blue wall and hold D (toward the wall) to wall-run.\nJump just before you grab it to keep your height." });

        // ---- 8. Wall jump corridor (+3 m climb) ----------------------------------------------
        Graybox.Box(course, "Corridor_Left", new Vector3(-2.5f, 3f, 121f), new Vector3(0.5f, 14f, 20f), Graybox.WallRun, wallRun: true);
        Graybox.Box(course, "Corridor_Right", new Vector3(2.5f, 3f, 121f), new Vector3(0.5f, 14f, 20f), Graybox.WallRun, wallRun: true);
        Platform(course, "P7_CorridorLanding", 131f, 139f, 4f);
        stations.Add(new Station { title = "Wall Jump", zStart = 103f, zEnd = 111f, floorY = 1f,
            text = "Wall-run, then press SPACE to jump to the opposite wall.\nChain wall jumps to climb up to the high platform." });

        // ---- 9. Spell range (5 targets behind a gate) ----------------------------------------
        Platform(course, "P8_Range", 139f, 165f, 4f, width: 20f);
        var dummies = new List<TrainingDummy>();
        float[] xs = { -7f, -3.5f, 0f, 3.5f, 7f };
        for (int i = 0; i < xs.Length; i++)
            dummies.Add(Dummy(course, $"Target_{i + 1}", new Vector3(xs[i], 4f + 1.6f + (i % 2) * 1.2f, 158f + (i % 2) * 2f)));
        var gateGO = Graybox.Box(course, "Gate", new Vector3(0f, 4f + 3f, 164.75f), new Vector3(20f, 6f, 0.5f), Graybox.Door);
        gateGO.isStatic = false;   // it moves when opening
        var gate = gateGO.AddComponent<TutorialGate>();
        SetArray(gate, "targets", dummies.Select(d => (Object)d.gameObject).ToArray());
        stations.Add(new Station { title = "Cast", zStart = 139f, zEnd = 150f, floorY = 4f, gateRef = gate,
            text = "LEFT CLICK to cast a fireball from your hand.\nDestroy all 5 targets to open the gate." });

        // ---- 10. Parry (a sentinel that only parried bolts can hurt) ----------------------------
        Platform(course, "P9_ParryRange", 165f, 195f, 4f, width: 14f);
        var sentinel = Sentinel(course, new Vector3(0f, 4f, 189f));
        Graybox.Box(course, "Sentinel_Pedestal", new Vector3(0f, 4.5f, 189f), new Vector3(3f, 1f, 3f), Graybox.Wall);
        var gate2GO = Graybox.Box(course, "Gate_Parry", new Vector3(0f, 4f + 3f, 194.75f), new Vector3(14f, 6f, 0.5f), Graybox.Door);
        gate2GO.isStatic = false;
        var gate2 = gate2GO.AddComponent<TutorialGate>();
        SetArray(gate2, "targets", new Object[] { sentinel });
        stations.Add(new Station { title = "Parry", zStart = 165f, zEnd = 172f, floorY = 4f, gateRef = gate2,
            text = "RIGHT CLICK just before a bolt hits to parry it back where you aim.\n" +
                   "Only parried bolts hurt the sentinel. Missing = 2s cooldown." });

        // ---- 11. Exit portal -----------------------------------------------------------------
        Platform(course, "P10_Exit", 195f, 207f, 4f);
        BuildPortal(course, new Vector3(0f, 4f, 203f));
        stations.Add(new Station { title = "Ready", zStart = 195f, zEnd = 200f, floorY = 4f,
            text = "That's everything. Step into the portal to enter the arena." });

        // ---- Steps, checkpoint pads, kill plane ----------------------------------------------
        var stepsRoot = Graybox.Group(null, "TutorialSteps");
        for (int i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            float zMid = (s.zStart + s.zEnd) * 0.5f;
            float w = s.title == "Cast" ? 20f : s.title == "Parry" ? 14f : Width;
            var trig = Graybox.Trigger(stepsRoot, $"Step_{i + 1:00}_{s.title.Replace(' ', '_')}",
                new Vector3(0f, s.floorY + 2.5f, zMid), new Vector3(w, 5f, s.zEnd - s.zStart));
            var step = trig.gameObject.AddComponent<TutorialStep>();
            step.order = i + 1;
            step.title = s.title;
            step.instructions = s.text;
            step.gate = s.gateRef;

            var spawn = new GameObject("Respawn").transform;
            spawn.SetParent(trig.transform, false);
            spawn.position = new Vector3(0f, s.floorY + 1.05f, s.zStart + 1.5f);
            spawn.rotation = Quaternion.identity;
            step.respawnPoint = spawn;

            // Green pad marks each checkpoint.
            if (i > 0) Graybox.Box(course, $"CheckpointPad_{i + 1:00}", new Vector3(0f, s.floorY + 0.03f, s.zStart + 1.5f), new Vector3(w - 0.5f, 0.06f, 1.2f), Graybox.Goal);
        }

        var kill = Graybox.Trigger(null, "KillVolume", new Vector3(0f, KillHeight, 90f), new Vector3(300f, 4f, 400f));
        kill.gameObject.AddComponent<KillVolume>();

        // ---- Player + manager ----------------------------------------------------------------
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -3f), Quaternion.identity);

        var managerGO = new GameObject("TutorialManager");
        var manager = managerGO.AddComponent<TutorialManager>();
        var mso = new SerializedObject(manager);
        mso.FindProperty("player").objectReferenceValue = player.GetComponent<FirstPersonController>();
        mso.ApplyModifiedPropertiesWithoutUndo();

        var light = GameObject.Find("Directional Light");
        if (light != null) light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath, ArenaScenePath);
        Debug.Log($"[TutorialBuilder] Built {ScenePath} with {stations.Count} stations.");
    }

    [MenuItem("Oxford Runner/Create Player Prefab")]
    public static void CreatePlayerPrefab()
    {
        var player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Create Player Prefab", "No FirstPersonController in the open scene.", "OK");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        PrefabUtility.SaveAsPrefabAssetAndConnect(player.gameObject, PlayerPrefabPath, InteractionMode.UserAction);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log($"[TutorialBuilder] Saved {PlayerPrefabPath} (scene instance is now linked to it).");
    }

    // ------------------------------------------------------------------ pieces
    /// <summary>A pillar platform spanning [zStart, zEnd] whose walkable top is at topY.</summary>
    private static void Platform(Transform parent, string name, float zStart, float zEnd, float topY, float width = Width)
    {
        float height = topY - PillarBottom;
        Graybox.Box(parent, name, new Vector3(0f, topY - height * 0.5f, (zStart + zEnd) * 0.5f),
                    new Vector3(width, height, zEnd - zStart), Graybox.Platform);
    }

    private static TrainingDummy Dummy(Transform parent, string name, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.GetComponent<MeshRenderer>().sharedMaterial = Graybox.Enemy;
        return go.AddComponent<TrainingDummy>();
    }

    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";

    /// <summary>Stationary enemy that keeps firing parryable bolts and only takes reflected damage.</summary>
    private static GameObject Sentinel(Transform parent, Vector3 pedestalTop)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "Sentinel";
        go.transform.SetPositionAndRotation(pedestalTop + Vector3.up * 2f, Quaternion.Euler(0f, 180f, 0f));
        var so = new SerializedObject(go.GetComponent<Enemy>());
        so.FindProperty("maxHealth").floatValue = 30f;          // one parried bolt (10 x 3)
        so.FindProperty("moveSpeed").floatValue = 0f;
        so.FindProperty("onlyReflectedDamage").boolValue = true;
        so.FindProperty("fireInterval").floatValue = 2.2f;
        so.FindProperty("windupTime").floatValue = 0.8f;
        so.FindProperty("attackRange").floatValue = 26f;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<Rigidbody>().isKinematic = true;
        return go;
    }

    private static void BuildPortal(Transform parent, Vector3 basePos)
    {
        var root = Graybox.Group(parent, "ExitPortal");
        Graybox.Box(root, "Frame_L", basePos + new Vector3(-2.25f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f), Graybox.Wall);
        Graybox.Box(root, "Frame_R", basePos + new Vector3(2.25f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f), Graybox.Wall);
        Graybox.Box(root, "Frame_Top", basePos + new Vector3(0f, 4.25f, 0f), new Vector3(5f, 0.5f, 0.5f), Graybox.Wall);
        var surface = Graybox.Box(root, "Surface", basePos + new Vector3(0f, 2f, 0f), new Vector3(4f, 4f, 0.2f), Graybox.Goal);
        var col = surface.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 6f);   // deep trigger so you can't miss it
        var exit = surface.AddComponent<SceneExit>();
        var so = new SerializedObject(exit);
        so.FindProperty("nextScene").stringValue = System.IO.Path.GetFileNameWithoutExtension(ArenaScenePath);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        var prop = so.FindProperty(field);
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddToBuildSettings(params string[] paths)
    {
        var list = EditorBuildSettings.scenes.ToList();
        foreach (var p in paths)
            if (!list.Any(s => s.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
        // Tutorial first so a fresh build starts there (until a main menu exists).
        list = list.OrderBy(s => s.path == ScenePath ? 0 : 1).ToList();
        EditorBuildSettings.scenes = list.ToArray();
    }
}
