using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Build Tutorial Scene
/// Generates Assets/Scenes/Tutorial.unity: a linear course through an enclosed dungeon hall (skylights,
/// torches, a dark pit below) that teaches one skill per station, then every spell, then parry.
/// Each station is a trigger (prompt + checkpoint); falling into the pit respawns at the last one.
/// Wall-runnable walls are teal wood with glowing rune strips (LevelBuilder.RunePanel); plain stone isn't.
///
/// The course runs along +Z:
///   jump 4 m | double-jump ledge 2.2 m | dash gap 10 m | slide tunnel | 16 m runway + slide-jump gap 15 m |
///   wall-run gap 14 m | wall-jump corridor (+3 m climb) | fireball targets | spell school (frost, lightning,
///   missiles, nova; a gate each) | parry a sentinel's bolt back into it | portal
/// </summary>
public static class TutorialBuilder
{
    private const string ScenePath = "Assets/Scenes/Tutorial.unity";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const float Width = 8f;             // standard platform width (X)
    private const float PillarBottom = LevelBuilder.DungeonPit;   // platforms are pillars rising from the pit floor
    private const float EndZ = 281f;
    private static LevelBuilder.DungeonMats mats;
    private const float KillHeight = -12f;

    private struct Station
    {
        public string title, text;
        public float zStart, zEnd;              // trigger span along Z
        public float floorY;                    // top of the floor the player stands on in this zone
        public float width;                     // trigger width (0 = standard platform width)
        public TutorialGate gateRef;            // optional gate whose progress the prompt shows
    }

    [MenuItem("Oxford Runner/Build Tutorial Scene")]
    public static void Build()
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            EditorUtility.DisplayDialog("Build Tutorial Scene",
                $"Missing {PlayerPrefabPath}.\nOpen a scene with the player and run 'Oxford Runner/Create Player Prefab' first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null) Object.DestroyImmediate(defaultCam);   // the player brings its own camera

        var course = Graybox.Group(null, "TutorialCourse");
        var stations = new List<Station>();
        mats = LevelBuilder.TutorialMaterials();
        LevelBuilder.TutorialShell(null, -12f, EndZ, mats);

        // ---- 1. Move -------------------------------------------------------------------------
        Platform(course, "P1_Start", -12f, 10f, 0f);
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
        Platform(course, "P4_Slide", 42f, 76f, 2.2f);
        Graybox.Box(course, "Tunnel_Roof", new Vector3(0f, 2.2f + 1.3f + 0.25f, 55f), new Vector3(Width, 0.5f, 10f), mats.stone);
        Graybox.Box(course, "Tunnel_Face", new Vector3(0f, 4f + 2.5f, 50.25f), new Vector3(Width, 5f, 0.5f), mats.stone);
        stations.Add(new Station { title = "Slide", zStart = 42f, zEnd = 50f, floorY = 2.2f,
            text = "Hold LEFT CTRL while running to slide.\nSlide under the low ceiling." });

        // ---- 6. Slide jump (16 m runway, 15 m gap) -------------------------------------------
        Platform(course, "P5_SlideJumpLanding", 91f, 99f, 2.2f);
        stations.Add(new Station { title = "Slide Jump", zStart = 60f, zEnd = 76f, floorY = 2.2f,
            text = "Build speed down the runway, slide, then press SPACE to slide-jump.\nIt launches you much further than a normal jump." });

        // ---- 7. Wall run (14 m gap, rune wall on the right) ----------------------------------
        LevelBuilder.RunePanel(course, "WallRun_Wall", new Vector3(Width * 0.5f + 0.75f, 2f, 106f), new Vector3(0.5f, 12f, 18f), mats);
        Platform(course, "P6_WallRunLanding", 113f, 121f, 1f);
        stations.Add(new Station { title = "Wall Run", zStart = 91f, zEnd = 99f, floorY = 2.2f,
            text = "Walls with glowing runes can be run on.\nJump at the rune wall and hold D (toward it) to wall-run." });

        // ---- 8. Wall jump corridor (+3 m climb) ----------------------------------------------
        LevelBuilder.RunePanel(course, "Corridor_Left", new Vector3(-2.5f, 3f, 131f), new Vector3(0.5f, 14f, 20f), mats);
        LevelBuilder.RunePanel(course, "Corridor_Right", new Vector3(2.5f, 3f, 131f), new Vector3(0.5f, 14f, 20f), mats);
        Platform(course, "P7_CorridorLanding", 141f, 149f, 4f);
        stations.Add(new Station { title = "Wall Jump", zStart = 113f, zEnd = 121f, floorY = 1f,
            text = "Wall-run, then press SPACE to jump to the opposite wall.\nChain wall jumps to climb up to the high platform." });

        // ---- 9. Cast (5 targets behind a gate) -----------------------------------------------
        Platform(course, "P8_Range", 149f, 175f, 4f, width: 20f);
        var dummies = new List<TrainingDummy>();
        float[] xs = { -7f, -3.5f, 0f, 3.5f, 7f };
        for (int i = 0; i < xs.Length; i++)
            dummies.Add(Dummy(course, $"Target_{i + 1}", new Vector3(xs[i], 4f + 1.6f + (i % 2) * 1.2f, 168f + (i % 2) * 2f)));
        var gate = Gate(course, "Gate", 174.75f, 20f, dummies.Select(d => (Object)d.gameObject).ToArray());
        stations.Add(new Station { title = "Cast", zStart = 149f, zEnd = 160f, floorY = 4f, width = 20f, gateRef = gate,
            text = "LEFT CLICK to cast a fireball from your hand.\nDestroy all 5 targets to open the gate." });

        // ---- 10. Spell school: one room per spell, each behind its own gate -------------------
        Platform(course, "P9_SpellSchool", 175f, 235f, 4f, width: 20f);

        var frost = new List<Object>();
        foreach (float x in new[] { -4f, 4f }) frost.Add(PracticeEnemy(course, new Vector3(x, 4f, 185f), 40f));
        var gFrost = Gate(course, "Gate_Frost", 189.75f, 20f, frost.ToArray());
        stations.Add(new Station { title = "Frost Lance", zStart = 175f, zEnd = 181f, floorY = 4f, width = 20f, gateRef = gFrost,
            text = "Press 2 for FROST LANCE. It freezes enemies solid.\nFrozen enemies take double damage: freeze one, then switch to 1 and fireball it." });

        var storm = new List<Object>();
        foreach (var o in new[] { new Vector2(-1.4f, 0f), new Vector2(1.4f, 0f), new Vector2(0f, 1.4f), new Vector2(0f, -1.4f) })
            storm.Add(PracticeEnemy(course, new Vector3(o.x, 4f, 200f + o.y), 40f));
        var gStorm = Gate(course, "Gate_Lightning", 204.75f, 20f, storm.ToArray());
        stations.Add(new Station { title = "Lightning Strike", zStart = 190f, zEnd = 195f, floorY = 4f, width = 20f, gateRef = gStorm,
            text = "Press 3 for LIGHTNING STRIKE. It blasts a whole group and STUNS them.\nHit stunned or frozen enemies with another spell for a COMBO." });

        var flyers = new List<Object>();
        for (int i = 0; i < 3; i++)
            flyers.Add(Dummy(course, $"Flyer_{i + 1}", new Vector3(-6f + i * 6f, 4f + 6f + (i % 2) * 2.5f, 216f), 28f).gameObject);
        var gMissile = Gate(course, "Gate_Missiles", 219.75f, 20f, flyers.ToArray());
        stations.Add(new Station { title = "Arcane Missiles", zStart = 205f, zEnd = 210f, floorY = 4f, width = 20f, gateRef = gMissile,
            text = "Press 4 for ARCANE MISSILES. Hold the crosshair on a target until it LOCKS ON.\nLocked missiles chase their target, so they never miss." });

        var ring = new List<Object>();
        for (int i = 0; i < 6; i++)
        {
            float a = i * 60f * Mathf.Deg2Rad;
            ring.Add(PracticeEnemy(course, new Vector3(Mathf.Sin(a) * 4f, 4f, 228f + Mathf.Cos(a) * 4f), 40f));
        }
        var gNova = Gate(course, "Gate_Nova", 234.75f, 20f, ring.ToArray());
        stations.Add(new Station { title = "Arcane Nova", zStart = 220f, zEnd = 223f, floorY = 4f, width = 20f, gateRef = gNova,
            text = "Press 5 for ARCANE NOVA. It blasts everything around you away.\nWalk into the middle of the circle and let it go." });

        // ---- 11. Parry (a sentinel that only parried bolts can hurt) ----------------------------
        Platform(course, "P10_ParryRange", 235f, 265f, 4f, width: 14f);
        var sentinel = Sentinel(course, new Vector3(0f, 4f, 259f));
        Graybox.Box(course, "Sentinel_Pedestal", new Vector3(0f, 4.5f, 259f), new Vector3(3f, 1f, 3f), mats.stone);
        var gate2 = Gate(course, "Gate_Parry", 264.75f, 14f, new Object[] { sentinel });
        stations.Add(new Station { title = "Parry", zStart = 235f, zEnd = 242f, floorY = 4f, width = 14f, gateRef = gate2,
            text = "RIGHT CLICK raises a barrier for 0.3s. Bolts that hit it fly back where you aim.\n" +
                   "Only parried bolts hurt the sentinel. Missing = 2s cooldown." });

        // ---- 12. Exit portal -----------------------------------------------------------------
        Platform(course, "P11_Exit", 265f, EndZ, 4f);
        BuildPortal(course, new Vector3(0f, 4f, 273f));
        stations.Add(new Station { title = "Ready", zStart = 265f, zEnd = 270f, floorY = 4f,
            text = "That's everything. Step into the portal to begin." });

        // ---- Steps, checkpoint pads, kill plane ----------------------------------------------
        var stepsRoot = Graybox.Group(null, "TutorialSteps");
        for (int i = 0; i < stations.Count; i++)
        {
            var s = stations[i];
            float zMid = (s.zStart + s.zEnd) * 0.5f;
            float w = s.width > 0f ? s.width : Width;
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
            if (i > 0)
            {
                // Flush with the floor and no collider, so it never catches the player's feet.
                var pad = Graybox.Box(course, $"CheckpointPad_{i + 1:00}", new Vector3(0f, s.floorY - 0.02f, s.zStart + 1.5f), new Vector3(w - 0.5f, 0.06f, 1.2f), Graybox.Goal);
                Object.DestroyImmediate(pad.GetComponent<Collider>());
            }
        }

        var kill = Graybox.Trigger(null, "KillVolume", new Vector3(0f, KillHeight, EndZ * 0.5f), new Vector3(60f, 4f, EndZ + 40f));
        kill.gameObject.AddComponent<KillVolume>();

        // ---- Player + manager ----------------------------------------------------------------
        var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.transform.SetPositionAndRotation(new Vector3(0f, 1.05f, -3f), Quaternion.identity);

        var managerGO = new GameObject("TutorialManager");
        var manager = managerGO.AddComponent<TutorialManager>();
        var mso = new SerializedObject(manager);
        mso.FindProperty("player").objectReferenceValue = player.GetComponent<FirstPersonController>();
        mso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
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
                    new Vector3(width, height, zEnd - zStart), mats.platform);
    }

    private static TrainingDummy Dummy(Transform parent, string name, Vector3 pos, float health = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.GetComponent<MeshRenderer>().sharedMaterial = Graybox.Enemy;
        var dummy = go.AddComponent<TrainingDummy>();
        if (health > 0f)
        {
            var so = new SerializedObject(dummy);
            so.FindProperty("maxHealth").floatValue = health;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return dummy;
    }

    /// <summary>A real enemy (so it can be frozen, stunned and knocked back) that stands still and never attacks.</summary>
    private static GameObject PracticeEnemy(Transform parent, Vector3 floorPos, float health)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemy_Skeleton.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = "PracticeSkeleton";
        go.transform.SetPositionAndRotation(floorPos + Vector3.up * 0.05f, Quaternion.Euler(0f, 180f, 0f));
        var so = new SerializedObject(go.GetComponent<Enemy>());
        so.FindProperty("maxHealth").floatValue = health;
        so.FindProperty("moveSpeed").floatValue = 0f;
        so.FindProperty("attackRange").floatValue = 0f;
        so.FindProperty("fireInterval").floatValue = 9999f;
        so.FindProperty("melee").boolValue = false;   // no swings, even when you nova up close
        so.ApplyModifiedPropertiesWithoutUndo();
        return go;
    }

    /// <summary>A door across the course at z that sinks once every target is dead.</summary>
    private static TutorialGate Gate(Transform parent, string name, float z, float width, Object[] targets)
    {
        var go = Graybox.Box(parent, name, new Vector3(0f, 4f + 3f, z), new Vector3(width, 6f, 0.5f), Graybox.Door);
        go.isStatic = false;   // it moves when opening
        var gate = go.AddComponent<TutorialGate>();
        SetArray(gate, "targets", targets);
        return gate;
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
        Graybox.Box(root, "Frame_L", basePos + new Vector3(-2.25f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f), mats.stone);
        Graybox.Box(root, "Frame_R", basePos + new Vector3(2.25f, 2f, 0f), new Vector3(0.5f, 4f, 0.5f), mats.stone);
        Graybox.Box(root, "Frame_Top", basePos + new Vector3(0f, 4.25f, 0f), new Vector3(5f, 0.5f, 0.5f), mats.stone);
        var surface = Graybox.Box(root, "Surface", basePos + new Vector3(0f, 2f, 0f), new Vector3(4f, 4f, 0.2f), Graybox.Goal);
        var col = surface.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 6f);   // deep trigger so you can't miss it
        var exit = surface.AddComponent<SceneExit>();
        var so = new SerializedObject(exit);
        so.FindProperty("nextScene").stringValue = "";   // GameFlow picks what comes next
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
