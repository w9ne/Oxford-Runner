using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Build Level 1 / Build Level 2
/// Generates the campaign levels (untextured, flat colours). Each level is split into encounter areas:
/// walking into one starts its waves; caster "garrisons" (towers, watchtowers) are already in place.
/// Clearing every area opens the exit portal (LevelManager). Deterministic and safe to re-run.
///
///   Level 1 "Greenland": grass islands over a void. Pits crossed by wall-running, square towers that
///                         each house one caster, rocks for cover, enemies on every island.
///   Level 2 "Castle Grounds": walled courtyards outside a castle. Lots of cylindrical towers to wall-run
///                         around, watchtowers with casters, barricade lines enemies hide behind.
/// </summary>
public static class LevelBuilder
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string EnemyPrefabPath = "Assets/Prefabs/Enemy.prefab";
    private const string MatDir = "Assets/Materials/Levels/";

    // ------------------------------------------------------------------ shared state for one build
    private static Transform root;
    private static List<LevelManager.Encounter> encounters;
    private static Enemy enemyPrefab;
    private static Material groundMat, cliffMat, stoneMat, wallRunMat, rockMat, woodMat, roofMat, portalMat;

    // ================================================================== LEVEL 1: GREENLAND
    [MenuItem("Oxford Runner/Build Level 1 (Greenland)")]
    public static void BuildLevel1()
    {
        if (!Begin("Level1", "Greenland")) return;
        groundMat = Mat("L1_Grass", new Color(0.38f, 0.62f, 0.3f));
        cliffMat = Mat("L1_Cliff", new Color(0.42f, 0.36f, 0.28f));
        stoneMat = Mat("L1_TowerStone", new Color(0.62f, 0.62f, 0.58f));
        wallRunMat = Mat("L1_WallRun", new Color(0.35f, 0.55f, 0.8f));
        rockMat = Mat("L1_Rock", new Color(0.5f, 0.52f, 0.48f));
        roofMat = Mat("L1_Roof", new Color(0.55f, 0.25f, 0.2f));
        portalMat = Mat("Portal", new Color(0.4f, 0.9f, 1f));
        SetAtmosphere(new Color(0.7f, 0.83f, 0.95f), 0.0018f, Quaternion.Euler(50f, -30f, 0f), new Color(1f, 0.96f, 0.88f));

        // --- A: start island
        var a = Group("A_Start");
        Island(a, new Vector3(0f, 0f, 0f), new Vector2(24f, 30f));
        Rock(a, new Vector3(-7f, 0f, 4f), new Vector3(2.5f, 1.5f, 2f), 20f);
        Rock(a, new Vector3(8f, 0f, -6f), new Vector3(2f, 2.5f, 2f), -15f);
        SlideTunnel(a, new Vector3(0f, 0f, 7f), 8f, 5f, 0f);          // slide out to the first pit

        // --- Pit 1 (18 m): twin wall-run walls either side of the gap.
        var p1 = Group("Pit1_TwinWalls");
        WallRunWall(p1, new Vector3(-5f, 0f, 24f), 26f, 0f);
        WallRunWall(p1, new Vector3(5f, 0f, 24f), 26f, 0f);

        // --- B: meadow (2 towers, 2 waves)
        var b = Group("B_Meadow");
        Island(b, new Vector3(0f, 0f, 58f), new Vector2(50f, 50f));
        var bGarrison = new List<Enemy>
        {
            Tower(b, new Vector3(-17f, 0f, 50f), 6f, 90f),
            Tower(b, new Vector3(17f, 0f, 72f), 8f, 160f),
        };
        Rock(b, new Vector3(-6f, 0f, 44f), new Vector3(3f, 1.6f, 2f), 10f);
        Rock(b, new Vector3(7f, 0f, 52f), new Vector3(2.5f, 2f, 3f), -25f);
        Rock(b, new Vector3(-9f, 0f, 66f), new Vector3(4f, 1.4f, 2f), 40f);
        Rock(b, new Vector3(4f, 0f, 74f), new Vector3(2f, 2.2f, 2f), 5f);
        FreeWall(b, new Vector3(-14f, 0f, 62f), 9f, 90f);
        WallRunCorridor(b, new Vector3(8f, 0f, 62f), 14f, 6f, 6f, 0f);
        FreeWall(b, new Vector3(-20f, 0f, 64f), 12f, 0f);
        LowBeam(b, new Vector3(0f, 0f, 47f), 6f, 0f);
        LowBeam(b, new Vector3(-8f, 0f, 72f), 6f, 30f);
        SlideTunnel(b, new Vector3(0f, 0f, 78f), 6f, 5f, 0f);         // slide out to pit 2
        Cubby(b, new Vector3(-1f, 0f, 57f), 180f, stoneMat);
        Cubby(b, new Vector3(15f, 0f, 45f), 200f, stoneMat);
        Cubby(b, new Vector3(-12f, 0f, 40f), 160f, stoneMat);
        Encounter("Meadow", b, new Vector3(0f, 0f, 58f), new Vector2(50f, 50f), bGarrison,
                  new[] { new Vector3(-18f, 0f, 78f), new Vector3(18f, 0f, 42f), new Vector3(-9f, 0f, 81f), new Vector3(-20f, 0f, 38f) },
                  Wave(3, 50f, 3.5f, 3f), Wave(4, 50f, 3.6f, 2.8f));

        // --- Pit 2 (20 m): long wall on the left with a small rest pillar mid-way.
        var p2 = Group("Pit2_LongWall");
        WallRunWall(p2, new Vector3(-5f, 0f, 93f), 28f, 0f);
        WallRunWall(p2, new Vector3(7f, 0f, 93f), 28f, 0f);          // second route on the right
        Island(p2, new Vector3(1f, 0f, 93f), new Vector2(3f, 3f), "RestPillar");

        // --- C: hills (3 towers incl. a tall central one, terraces, wall-run slabs, 3 waves)
        var c = Group("C_Hills");
        Island(c, new Vector3(0f, 0f, 133f), new Vector2(60f, 60f));
        var cGarrison = new List<Enemy>
        {
            Tower(c, new Vector3(-22f, 0f, 112f), 7f, 60f),
            Tower(c, new Vector3(22f, 0f, 122f), 9f, 250f),
            Tower(c, new Vector3(0f, 0f, 152f), 12f, 180f),
        };
        Terrace(c, new Vector3(-14f, 0f, 135f), new Vector3(12f, 2f, 10f));
        Terrace(c, new Vector3(-14f, 2f, 135f), new Vector3(6f, 2f, 5f));
        Terrace(c, new Vector3(15f, 0f, 145f), new Vector3(10f, 2.5f, 12f));
        FreeWall(c, new Vector3(-4f, 0f, 118f), 10f, 30f);
        FreeWall(c, new Vector3(8f, 0f, 132f), 10f, -30f);
        FreeWall(c, new Vector3(-22f, 0f, 150f), 10f, 90f);
        Rock(c, new Vector3(4f, 0f, 110f), new Vector3(3f, 1.5f, 2.5f), 15f);
        Rock(c, new Vector3(-6f, 0f, 145f), new Vector3(2.5f, 2f, 2.5f), -10f);
        Rock(c, new Vector3(20f, 0f, 106f), new Vector3(2f, 1.8f, 3f), 60f);
        Rock(c, new Vector3(-12f, 0f, 158f), new Vector3(3f, 1.5f, 2f), 0f);
        WallRunCorridor(c, new Vector3(-26f, 0f, 140f), 14f, 5f, 7f, 0f);
        WallRunCorridor(c, new Vector3(24f, 0f, 108f), 12f, 5f, 6f, 90f);
        LowBeam(c, new Vector3(-4f, 0f, 128f), 6f, 0f);
        LowBeam(c, new Vector3(16f, 0f, 132f), 6f, 90f);
        SlideTunnel(c, new Vector3(11f, 0f, 156f), 8f, 4f, 0f);       // slide out to pit 3
        Cubby(c, new Vector3(-12f, 0f, 108f), 180f, stoneMat);
        Cubby(c, new Vector3(6f, 0f, 141f), 180f, stoneMat);
        Cubby(c, new Vector3(-18f, 0f, 124f), 200f, stoneMat);
        var hills = Encounter("Hills", c, new Vector3(0f, 0f, 133f), new Vector2(60f, 60f), cGarrison,
                  new[] { new Vector3(-25f, 0f, 160f), new Vector3(25f, 0f, 160f), new Vector3(-26f, 0f, 125f), new Vector3(26f, 0f, 140f) },
                  Wave(3, 55f, 3.8f, 2.8f), Wave(4, 55f, 3.8f, 2.6f), Wave(5, 60f, 4f, 2.5f));
        hills.gates.Add(AreaGate(Group("Gate_MeadowToHills"), 84f, 80f));     // across the far edge of the meadow

        // --- Pit 3 (25 m): staggered walls, run the right one then jump across to the left one.
        var p3 = Group("Pit3_Staggered");
        WallRunWall(p3, new Vector3(5f, 0f, 168f), 18f, 0f);
        WallRunWall(p3, new Vector3(-5f, 0f, 183f), 18f, 0f);
        Island(p3, new Vector3(0f, 0f, 176f), new Vector2(3f, 3f), "RestPillar");

        // --- D: gate island (1 tower, final wave, portal)
        var d = Group("D_Gate");
        Island(d, new Vector3(0f, 0f, 203f), new Vector2(30f, 30f));
        var dGarrison = new List<Enemy> { Tower(d, new Vector3(10f, 0f, 198f), 6f, 300f) };
        Rock(d, new Vector3(-9f, 0f, 205f), new Vector3(3f, 1.6f, 2f), 25f);
        SlideTunnel(d, new Vector3(-3f, 0f, 194f), 6f, 4f, 0f);
        FreeWall(d, new Vector3(-12f, 0f, 200f), 10f, 0f);
        Rock(d, new Vector3(4f, 0f, 208f), new Vector3(2f, 1.5f, 2f), -20f);
        Cubby(d, new Vector3(-8f, 0f, 190f), 180f, stoneMat);
        Cubby(d, new Vector3(9f, 0f, 209f), 180f, stoneMat);
        var gateArea = Encounter("Gate", d, new Vector3(0f, 0f, 203f), new Vector2(30f, 30f), dGarrison,
                  new[] { new Vector3(-12f, 0f, 214f), new Vector3(12f, 0f, 214f) },
                  Wave(4, 60f, 4f, 2.4f));
        gateArea.gates.Add(AreaGate(Group("Gate_HillsToGate"), 164f, 90f));  // across the far edge of the hills
        var portal = Portal(d, new Vector3(0f, 0f, 213f), 0f);

        IslandBounds();
        GreenlandScenery();
        Finish("Level 1 - Greenland", new Vector3(0f, 0f, -10f), 0f, portal, killY: -10f, killCenterZ: 100f);
    }

    // ================================================================== LEVEL 2: CASTLE GROUNDS
    [MenuItem("Oxford Runner/Build Level 2 (Castle Grounds)")]
    public static void BuildLevel2()
    {
        if (!Begin("Level2", "Castle Grounds")) return;
        groundMat = Mat("L2_Ground", new Color(0.45f, 0.5f, 0.35f));
        cliffMat = Mat("L2_Dirt", new Color(0.4f, 0.33f, 0.26f));
        stoneMat = Mat("L2_CastleStone", new Color(0.55f, 0.55f, 0.6f));
        wallRunMat = Mat("L2_TowerWallRun", new Color(0.42f, 0.5f, 0.72f));
        rockMat = Mat("L2_Rock", new Color(0.48f, 0.48f, 0.5f));
        woodMat = Mat("L2_Barricade", new Color(0.5f, 0.34f, 0.2f));
        roofMat = Mat("L2_Roof", new Color(0.3f, 0.32f, 0.45f));
        portalMat = Mat("Portal", new Color(0.4f, 0.9f, 1f));
        SetAtmosphere(new Color(0.85f, 0.75f, 0.62f), 0.004f, Quaternion.Euler(28f, 60f, 0f), new Color(1f, 0.85f, 0.65f));

        const float halfW = 55f;
        var grounds = Group("Grounds");
        Box(grounds, "Ground", new Vector3(0f, -0.5f, 90f), new Vector3(halfW * 2f + 10f, 1f, 220f), groundMat);
        // Curtain walls around the grounds (wall-runnable on the inside).
        CastleWall(grounds, new Vector3(-halfW - 1.5f, 0f, 90f), 210f, 90f, 14f);
        CastleWall(grounds, new Vector3(halfW + 1.5f, 0f, 90f), 210f, 90f, 14f);
        CastleWall(grounds, new Vector3(0f, 0f, -16f), halfW * 2f + 6f, 0f, 14f);
        CastleWall(grounds, new Vector3(0f, 0f, 196f), halfW * 2f + 6f, 0f, 18f);   // the castle itself, out of reach

        // --- Outer field: scattered round towers, two watchtowers, loose barricades.
        var o = Group("A_OuterField");
        var oGarrison = new List<Enemy>
        {
            WatchTower(o, new Vector3(-30f, 0f, 30f), 2.2f, 9f),
            WatchTower(o, new Vector3(32f, 0f, 22f), 2.2f, 10f),
        };
        RoundTower(o, new Vector3(-12f, 0f, 14f), 2.5f, 12f);
        RoundTower(o, new Vector3(14f, 0f, 36f), 3f, 14f);
        RoundTower(o, new Vector3(-40f, 0f, 6f), 2.5f, 10f);
        RoundTower(o, new Vector3(42f, 0f, 44f), 2.5f, 12f);
        RoundTower(o, new Vector3(0f, 0f, 30f), 2f, 11f);
        BarricadeLine(o, new Vector3(-18f, 0f, 38f), 3, 0f);
        BarricadeLine(o, new Vector3(20f, 0f, 46f), 3, 10f);
        Barricade(o, new Vector3(-4f, 0f, 44f), 15f);
        Barricade(o, new Vector3(30f, 0f, 8f), 80f);
        SlideTunnel(o, new Vector3(0f, 0f, 2f), 8f, 5f, 0f);
        ConnectWall(o, new Vector3(-12f, 0f, 14f), new Vector3(0f, 0f, 30f), 7f);
        ConnectWall(o, new Vector3(0f, 0f, 30f), new Vector3(14f, 0f, 36f), 7f);
        ConnectWall(o, new Vector3(-40f, 0f, 6f), new Vector3(-30f, 0f, 30f), 6f);
        LowBeam(o, new Vector3(-26f, 0f, 48f), 6f, 0f, woodMat);
        LowBeam(o, new Vector3(28f, 0f, 34f), 6f, 90f, woodMat);
        Encounter("Outer Field", o, new Vector3(0f, 0f, 25f), new Vector2(halfW * 2f, 70f), oGarrison,
                  new[] { new Vector3(-20f, 0f, 52f), new Vector3(20f, 0f, 54f), new Vector3(-45f, 0f, 45f), new Vector3(45f, 0f, 30f) },
                  Wave(4, 50f, 3.6f, 3f), Wave(5, 50f, 3.8f, 2.8f));

        // --- Inner wall with a gate between two big round towers.
        var w1 = Group("InnerWall_1");
        var door1 = GatedWall(w1, 62f, halfW, gateWidth: 10f, height: 11f);

        // --- Inner yard: a grid of round towers to weave through, barricade rows.
        var y = Group("B_InnerYard");
        var yGarrison = new List<Enemy>
        {
            WatchTower(y, new Vector3(-36f, 0f, 100f), 2.4f, 11f),
            WatchTower(y, new Vector3(36f, 0f, 92f), 2.4f, 11f),
        };
        var rng = new System.Random(20261123);
        for (int row = 0; row < 3; row++)
        {
            var placed = new Vector3?[4];
            for (int col = 0; col < 4; col++)
            {
                float x = -27f + col * 18f + (row % 2 == 0 ? 0f : 9f);
                float z = 76f + row * 16f;
                var pos = new Vector3(x, 0f, z);
                if (Mathf.Abs(x) < 4f && row == 0) continue;   // keep the gate approach open
                if (Vector3.Distance(pos, new Vector3(-36f, 0f, 100f)) < 8f || Vector3.Distance(pos, new Vector3(36f, 0f, 92f)) < 8f) continue;
                RoundTower(y, pos, 2f + (float)rng.NextDouble() * 1.2f, 9f + (float)rng.NextDouble() * 6f);
                placed[col] = pos;
            }
            // Link towers in pairs with wall-run slabs; the gaps between pairs stay open on foot.
            for (int col = 0; col < 4; col += 2)
                if (placed[col].HasValue && placed[col + 1].HasValue)
                    ConnectWall(y, placed[col].Value, placed[col + 1].Value, 7f);
        }
        LowBeam(y, new Vector3(0f, 0f, 120f), 6f, 0f, woodMat);
        LowBeam(y, new Vector3(-40f, 0f, 112f), 6f, 90f, woodMat);
        BarricadeLine(y, new Vector3(-20f, 0f, 118f), 4, 0f);
        BarricadeLine(y, new Vector3(22f, 0f, 116f), 4, 0f);
        BarricadeLine(y, new Vector3(0f, 0f, 104f), 2, 90f);
        Barricade(y, new Vector3(-44f, 0f, 80f), 60f);
        Barricade(y, new Vector3(44f, 0f, 120f), -60f);
        var yard = Encounter("Inner Yard", y, new Vector3(0f, 0f, 98f), new Vector2(halfW * 2f, 66f), yGarrison,
                  new[] { new Vector3(-40f, 0f, 126f), new Vector3(40f, 0f, 126f), new Vector3(0f, 0f, 128f), new Vector3(-48f, 0f, 96f), new Vector3(48f, 0f, 104f) },
                  Wave(4, 55f, 3.8f, 2.8f), Wave(5, 55f, 4f, 2.6f), Wave(5, 60f, 4f, 2.4f));
        yard.gates.Add(door1);
        yard.gates.Add(AreaGate(Group("Gate_OuterToYard"), 62f, halfW * 2f + 10f, visible: false));

        // --- Second wall and the keep approach.
        var w2 = Group("InnerWall_2");
        var door2 = GatedWall(w2, 136f, halfW, gateWidth: 12f, height: 12f);

        var k = Group("C_KeepApproach");
        var kGarrison = new List<Enemy>
        {
            WatchTower(k, new Vector3(-30f, 0f, 176f), 2.6f, 13f),
            WatchTower(k, new Vector3(30f, 0f, 176f), 2.6f, 13f),
        };
        RoundTower(k, new Vector3(-16f, 0f, 150f), 3f, 14f);
        RoundTower(k, new Vector3(16f, 0f, 150f), 3f, 14f);
        RoundTower(k, new Vector3(-42f, 0f, 160f), 3.5f, 12f);
        RoundTower(k, new Vector3(42f, 0f, 160f), 3.5f, 12f);
        // Barricade maze in front of the castle.
        BarricadeLine(k, new Vector3(-12f, 0f, 162f), 3, 0f);
        BarricadeLine(k, new Vector3(12f, 0f, 168f), 3, 0f);
        BarricadeLine(k, new Vector3(-24f, 0f, 172f), 2, 90f);
        BarricadeLine(k, new Vector3(24f, 0f, 158f), 2, 90f);
        Barricade(k, new Vector3(0f, 0f, 156f), 0f);
        ConnectWall(k, new Vector3(-16f, 0f, 150f), new Vector3(-42f, 0f, 160f), 8f);
        ConnectWall(k, new Vector3(16f, 0f, 150f), new Vector3(42f, 0f, 160f), 8f);
        LowBeam(k, new Vector3(0f, 0f, 174f), 8f, 0f, woodMat);
        var keep = Encounter("Keep Approach", k, new Vector3(0f, 0f, 165f), new Vector2(halfW * 2f, 56f), kGarrison,
                  new[] { new Vector3(-46f, 0f, 186f), new Vector3(46f, 0f, 186f), new Vector3(-20f, 0f, 188f), new Vector3(20f, 0f, 188f) },
                  Wave(5, 60f, 4.2f, 2.4f), Wave(6, 65f, 4.2f, 2.2f));
        keep.gates.Add(door2);
        keep.gates.Add(AreaGate(Group("Gate_YardToKeep"), 136f, halfW * 2f + 10f, visible: false));
        // Cubbies to duck into (same spots as the hand-edited scene got).
        var cub = Group("Cubbies");
        foreach (var c in new[] { new Vector3(-9f, 0f, 45f), new Vector3(13f, 0f, 10f), new Vector3(-37f, 0f, 48f),
                                  new Vector3(31f, 0f, 93f), new Vector3(-26f, 0f, 97f), new Vector3(36f, 0f, 71f),
                                  new Vector3(-2f, 0f, 169f), new Vector3(-31f, 0f, 148f), new Vector3(26f, 0f, 148f) })
            Cubby(cub, c, 180f, stoneMat);
        var portal = Portal(k, new Vector3(0f, 0f, 184f), 0f);

        Finish("Level 2 - Castle Grounds", new Vector3(0f, 0f, -8f), 0f, portal, killY: -12f, killCenterZ: 90f);
    }

    // ================================================================== build plumbing
    private static string scenePath;

    private static bool Begin(string sceneName, string label)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Build " + label, "Missing " + PlayerPrefabPath, "OK");
            return false;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        if (!AssetDatabase.IsValidFolder("Assets/Materials/Levels")) AssetDatabase.CreateFolder("Assets/Materials", "Levels");

        scenePath = $"Assets/Scenes/{sceneName}.unity";
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var cam = GameObject.Find("Main Camera");
        if (cam != null) Object.DestroyImmediate(cam);   // the player brings its own camera

        root = new GameObject("Level").transform;
        encounters = new List<LevelManager.Encounter>();
        enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath).GetComponent<Enemy>();
        return true;
    }

    private static void Finish(string levelName, Vector3 start, float yaw, GameObject portal, float killY, float killCenterZ)
    {
        var scene = EditorSceneManager.GetActiveScene();

        var startT = new GameObject("PlayerStart").transform;
        startT.SetParent(root, false);
        startT.SetPositionAndRotation(start + Vector3.up * 1.05f, Quaternion.Euler(0f, yaw, 0f));

        var player = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath), scene);
        player.transform.SetPositionAndRotation(startT.position, startT.rotation);

        var kill = Graybox.Trigger(root, "KillVolume", new Vector3(0f, killY - 2f, killCenterZ), new Vector3(600f, 4f, 600f));
        kill.gameObject.AddComponent<KillVolume>();

        var manager = new GameObject("LevelManager").AddComponent<LevelManager>();
        manager.Configure(levelName, enemyPrefab, startT, portal, encounters);
        manager.ConfigureTypes(EnemySetup.Load(EnemySetup.SkeletonPath), EnemySetup.Load(EnemySetup.MagePath),
                               EnemySetup.Load(EnemySetup.CommanderPath));
        EditorUtility.SetDirty(manager);

        EditorSceneManager.SaveScene(scene, scenePath);
        AddToBuild(scenePath);
        Debug.Log($"[LevelBuilder] Built {scenePath}: {encounters.Count} areas, " +
                  $"{encounters.Sum(e => e.garrison.Count)} garrison casters, {encounters.Sum(e => e.waves.Sum(w => w.enemyCount))} wave enemies.");
    }

    private static void AddToBuild(string path)
    {
        var list = EditorBuildSettings.scenes.ToList();
        if (list.Any(s => s.path == path)) return;
        // Keep the campaign order: insert after the last existing campaign scene.
        list.Add(new EditorBuildSettingsScene(path, true));
        string[] order = { "MainMenu", "Tutorial", "Level1", "Level2", "Level3", "MovementTest" };
        list = list.OrderBy(s =>
        {
            int i = System.Array.IndexOf(order, System.IO.Path.GetFileNameWithoutExtension(s.path));
            return i < 0 ? 99 : i;
        }).ToList();
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ================================================================== scenery (Level 1)
    private const string Nature = "Assets/RPGPP_LT/Prefabs/Nature/";
    private const string SNP = "Assets/SimpleNaturePack/Prefabs/";

    /// <summary>Places a decorative prefab (unpacked, static). Colliders are kept only if asked.</summary>
    private static GameObject Prop(Transform parent, string path, Vector3 pos, float yaw, float scale, bool keepColliders = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) { Debug.LogWarning("[LevelBuilder] Missing prop " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one * scale;
        if (!keepColliders) foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        foreach (var t in go.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        return go;
    }

    /// <summary>
    /// Greenland dressing: a few trees and bushes on the islands, rolling hills far below the
    /// islands (the valley floor) and a ring of mountains on the horizon, plus clouds.
    /// None of the backdrop has colliders; it sits below the kill volume or far outside the play area.
    /// </summary>
    /// <summary>
    /// Level 1 edges: a wooden fence 2.5 m in from every island edge (gaps where the pit crossings start),
    /// an invisible wall along it so you can't jump out, and flowers / bushes / small trees planted in the
    /// strip behind the fence as background.
    /// </summary>
    private static void IslandBounds()
    {
        var g = Group("Bounds_Fences");
        var rng = new System.Random(77);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const string Fence = "Assets/RPGPP_LT/Prefabs/Exterior/Wood_path/rpgpp_lt_fence_wood_01a.prefab";
        const float inset = 2.5f, gap = 10f, fenceLen = 3.6f;
        string[] plants = { Nature + "Vegetation/Flowers/rpgpp_lt_flower_01.prefab", Nature + "Vegetation/Flowers/rpgpp_lt_flower_02.prefab",
                            Nature + "Vegetation/Flowers/rpgpp_lt_flower_03.prefab", Nature + "Vegetation/Bushes/rpgpp_lt_bush_01.prefab",
                            Nature + "Vegetation/Bushes/rpgpp_lt_bush_02.prefab", Nature + "Vegetation/Grass/rpgpp_lt_grass_small_01a.prefab",
                            SNP + "Flowers_01.prefab", SNP + "Flowers_02.prefab", SNP + "Bush_01.prefab", SNP + "Grass_01.prefab" };
        string[] smallTrees = { Nature + "Vegetation/Trees/rpgpp_lt_tree_01.prefab", Nature + "Vegetation/Trees/rpgpp_lt_tree_pine_01.prefab", SNP + "Tree_02.prefab" };

        // centre, half size, open south edge?, open north edge?
        var islands = new[]
        {
            (new Vector3(0f, 0f, 0f), new Vector2(12f, 15f), false, true),
            (new Vector3(0f, 0f, 58f), new Vector2(25f, 25f), true, true),
            (new Vector3(0f, 0f, 133f), new Vector2(30f, 30f), true, true),
            (new Vector3(0f, 0f, 203f), new Vector2(15f, 15f), true, false),
        };
        foreach (var (c, half, openSouth, openNorth) in islands)
        {
            float fx = half.x - inset, fz = half.y - inset;
            // west / east sides (run along z), south / north sides (run along x)
            FenceLine(g, c + new Vector3(-fx, 0f, -fz), c + new Vector3(-fx, 0f, fz), Fence, fenceLen);
            FenceLine(g, c + new Vector3(fx, 0f, -fz), c + new Vector3(fx, 0f, fz), Fence, fenceLen);
            for (int e = 0; e < 2; e++)
            {
                float z = e == 0 ? -fz : fz;
                bool open = e == 0 ? openSouth : openNorth;
                if (open)
                {
                    FenceLine(g, c + new Vector3(-fx, 0f, z), c + new Vector3(-gap, 0f, z), Fence, fenceLen);
                    FenceLine(g, c + new Vector3(gap, 0f, z), c + new Vector3(fx, 0f, z), Fence, fenceLen);
                }
                else FenceLine(g, c + new Vector3(-fx, 0f, z), c + new Vector3(fx, 0f, z), Fence, fenceLen);
            }

            // Background planting in the strip between fence and edge (never on the play area).
            float perimeter = 4f * (half.x + half.y);
            int count = Mathf.RoundToInt(perimeter / 2.2f);
            for (int i = 0; i < count; i++)
            {
                float t = R(0f, 1f) * perimeter, d = R(0.6f, inset - 0.5f);
                Vector3 p;
                if (t < 2f * half.x) p = new Vector3(-half.x + t, 0f, -half.y + d);
                else if (t < 4f * half.x) p = new Vector3(-half.x + (t - 2f * half.x), 0f, half.y - d);
                else if (t < 4f * half.x + 2f * half.y) p = new Vector3(-half.x + d, 0f, -half.y + (t - 4f * half.x));
                else p = new Vector3(half.x - d, 0f, -half.y + (t - 4f * half.x - 2f * half.y));
                if (Mathf.Abs(p.x) < gap && Mathf.Abs(Mathf.Abs(p.z) - half.y) < inset + 0.1f) continue;   // keep crossings clear
                bool tree = rng.NextDouble() < 0.15;
                string kind = tree ? smallTrees[rng.Next(smallTrees.Length)] : plants[rng.Next(plants.Length)];
                Prop(g, kind, c + p, R(0f, 360f), tree ? R(0.6f, 0.85f) : R(0.8f, 1.3f));
            }
        }
    }

    // Fence panels from a to b, plus an invisible 30 m wall along it (no jumping out of bounds).
    private static void FenceLine(Transform parent, Vector3 a, Vector3 b, string fencePath, float panel)
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.5f) return;
        Quaternion rot = Quaternion.LookRotation(d / len) * Quaternion.Euler(0f, 90f, 0f);   // fence model runs along x
        int n = Mathf.Max(1, Mathf.RoundToInt(len / panel));
        float scale = len / n / 3f;   // 3 m model, stretched to fit evenly
        for (int i = 0; i < n; i++)
        {
            var f = Prop(parent, fencePath, a + d * ((i + 0.5f) / n), 0f, 1f);
            if (f == null) continue;
            f.transform.rotation = rot;
            f.transform.localScale = new Vector3(scale, 1.2f, 1.2f);
        }
        var wall = new GameObject("BoundsWall");
        wall.transform.SetParent(parent, false);
        wall.transform.SetPositionAndRotation((a + b) * 0.5f + Vector3.up * 15f, Quaternion.LookRotation(d / len));
        var box = wall.AddComponent<BoxCollider>();
        box.size = new Vector3(0.4f, 30f, len);
        wall.isStatic = true;
    }

    private static void GreenlandScenery()
    {
        var rng = new System.Random(4242);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // --- Trees: only a handful per island, out by the edges so they don't block routes.
        var trees = Group("Scenery_Trees");
        string[] treeKinds = { SNP + "Tree_01.prefab", SNP + "Tree_03.prefab", SNP + "Tree_04.prefab", SNP + "Tree_05.prefab", Nature + "Vegetation/Trees/rpgpp_lt_tree_01.prefab", Nature + "Vegetation/Trees/rpgpp_lt_tree_pine_01.prefab" };
        Vector3[] treeSpots =
        {
            new Vector3(-10f, 0f, -12f), new Vector3(10f, 0f, 12f),                          // start
            new Vector3(-22f, 0f, 36f), new Vector3(22f, 0f, 56f), new Vector3(-21f, 0f, 80f), new Vector3(12f, 0f, 36f), // meadow
            new Vector3(-27f, 0f, 106f), new Vector3(27f, 0f, 110f), new Vector3(-27f, 0f, 160f), new Vector3(27f, 0f, 158f), new Vector3(6f, 0f, 120f), // hills
            new Vector3(-13f, 0f, 214f), new Vector3(13f, 0f, 190f),                         // gate
        };
        foreach (var spot in treeSpots)
        {
            string kind = treeKinds[rng.Next(treeKinds.Length)];
            float scale = kind.Contains("rpgpp") ? R(0.9f, 1.15f) : R(1.4f, 1.8f);
            Prop(trees, kind, spot, R(0f, 360f), scale, keepColliders: true);
        }
        // A few bushes and flowers for colour (no collision).
        string[] small = { Nature + "Vegetation/Bushes/rpgpp_lt_bush_01.prefab", Nature + "Vegetation/Flowers/rpgpp_lt_flower_01.prefab", Nature + "Vegetation/Flowers/rpgpp_lt_flower_02.prefab", Nature + "Vegetation/Grass/rpgpp_lt_grass_small_01b.prefab" };
        Vector3[] islandCentres = { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 58f), new Vector3(0f, 0f, 133f), new Vector3(0f, 0f, 203f) };
        Vector2[] islandHalf = { new Vector2(11f, 14f), new Vector2(24f, 24f), new Vector2(29f, 29f), new Vector2(14f, 14f) };
        for (int i = 0; i < islandCentres.Length; i++)
            for (int n = 0; n < 8; n++)
            {
                // Hug the island rim (outer 30%) so the middle stays clear.
                float sx = rng.NextDouble() < 0.5 ? -1f : 1f, sz = rng.NextDouble() < 0.5 ? -1f : 1f;
                var p = islandCentres[i] + new Vector3(sx * R(0.7f, 0.95f) * islandHalf[i].x, 0f, sz * R(0f, 0.95f) * islandHalf[i].y);
                Prop(trees, small[rng.Next(small.Length)], p, R(0f, 360f), R(0.8f, 1.2f));
            }

        // --- Foreground: rolling green hills far below the islands (the valley you'd fall into).
        var hills = Group("Scenery_Valley");
        Box(hills, "ValleyFloor", new Vector3(0f, -62f, 100f), new Vector3(4000f, 2f, 4000f), groundMat);
        string[] hillKinds = { Nature + "Terrain/rpgpp_lt_hill_small_01.prefab", Nature + "Terrain/rpgpp_lt_hill_small_02.prefab" };
        for (int i = 0; i < 18; i++)
        {
            float z = -60f + i * 18f;
            float side = i % 2 == 0 ? -1f : 1f;
            Prop(hills, hillKinds[i % 2], new Vector3(side * R(50f, 110f), -62f, z + R(-8f, 8f)), R(0f, 360f), R(0.8f, 1.4f));
        }
        for (int i = 0; i < 8; i++)
            Prop(hills, hillKinds[i % 2], new Vector3(R(-60f, 60f), -70f, R(-40f, 260f)), R(0f, 360f), R(0.6f, 1f));
        // Forest down in the valley, either side of the island chain (background only).
        for (int i = 0; i < 60; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            Prop(hills, treeKinds[rng.Next(treeKinds.Length)], new Vector3(side * R(40f, 140f), -61f, R(-60f, 280f)), R(0f, 360f), R(2.5f, 4f));
        }

        // --- Background: a ring of mountains on the horizon.
        var mountains = Group("Scenery_Mountains");
        Vector3 centre = new Vector3(0f, 0f, 100f);
        for (int i = 0; i < 14; i++)
        {
            float ang = i * (360f / 14f) + R(-8f, 8f);
            Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
            Prop(mountains, Nature + "Terrain/rpgpp_lt_mountain_01.prefab", centre + dir * R(330f, 420f) + Vector3.down * 70f, R(0f, 360f), R(1.6f, 2.4f));
        }

        // --- Clouds drifting around the islands' level.
        var sky = Group("Scenery_Clouds");
        for (int i = 0; i < 10; i++)
        {
            string kind = Nature + (i % 2 == 0 ? "Sky/rpgpp_lt_cloud_01.prefab" : "Sky/rpgpp_lt_cloud_02.prefab");
            float side = i % 2 == 0 ? -1f : 1f;
            Prop(sky, kind, new Vector3(side * R(70f, 160f), R(25f, 60f), R(-60f, 280f)), R(0f, 360f), R(0.8f, 1.5f));
        }
    }

    private static void SetAtmosphere(Color fog, float density, Quaternion sunRotation, Color sunColor)
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = density;
        var sun = GameObject.Find("Directional Light");
        if (sun != null)
        {
            sun.transform.rotation = sunRotation;
            var l = sun.GetComponent<Light>();
            l.color = sunColor;
            l.intensity = 1.1f;
            l.shadows = LightShadows.Soft;
        }
    }

    private static Material Mat(string name, Color color)
    {
        string path = MatDir + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", 0.1f);
        LevelTextures.Apply(mat, name);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ================================================================== encounters
    private static LevelManager.Wave Wave(int count, float health, float speed, float interval) =>
        new LevelManager.Wave { enemyCount = count, enemyHealth = health, enemySpeed = speed, enemyFireInterval = interval, spawnInterval = 0.8f };

    private static LevelManager.Encounter Encounter(string name, Transform parent, Vector3 center, Vector2 size, List<Enemy> garrison,
                                  Vector3[] spawns, params LevelManager.Wave[] waves)
    {
        var spawnRoot = Graybox.Group(parent, "SpawnPoints");
        var points = new Transform[spawns.Length];
        for (int i = 0; i < spawns.Length; i++)
        {
            var t = new GameObject($"Spawn_{i + 1}").transform;
            t.SetParent(spawnRoot, false);
            t.position = spawns[i] + Vector3.up * 1.05f;
            Vector3 look = center - spawns[i]; look.y = 0f;
            t.rotation = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.back);
            points[i] = t;
            // A dark pad shows where enemies come from.
            Box(spawnRoot, $"SpawnPad_{i + 1}", spawns[i] + Vector3.up * 0.03f, new Vector3(2.4f, 0.06f, 2.4f), Graybox.Enemy);
        }
        var e = new LevelManager.Encounter
        {
            name = name,
            triggerArea = new Bounds(center + Vector3.up * 8f, new Vector3(size.x, 40f, size.y)),
            spawnPoints = points,
            waves = waves.ToList(),
            garrison = garrison,
        };
        encounters.Add(e);
        return e;
    }

    /// <summary>
    /// A glowing barrier across the level at z, blocking the way into the next area until the current
    /// one is cleared (LevelManager sinks it). Tall enough that you can't wall-run over it.
    /// </summary>
    public static GameObject AreaGate(Transform parent, float z, float width, float height = 40f, bool visible = true)
    {
        var gate = Box(parent, "AreaGate", new Vector3(0f, height * 0.5f - 2f, z), new Vector3(width, height, 0.6f), BarrierMaterial());
        gate.isStatic = false;
        var r = gate.GetComponent<Renderer>();
        r.enabled = visible;   // invisible = just stops wall-running over the castle wall
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return gate;
    }

    public static Material BarrierMaterial()
    {
        string path = MatDir + "AreaGate.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.75f, 0.35f, 1f, 0.28f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>A stationary caster (normal damage) standing at the given floor point.</summary>
    private static Enemy Garrison(Transform parent, Vector3 floor)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab.gameObject, parent);
        go.name = "GarrisonCaster";
        go.transform.position = floor + Vector3.up * 1.02f;
        var so = new SerializedObject(go.GetComponent<Enemy>());
        so.FindProperty("maxHealth").floatValue = 20f;
        so.FindProperty("moveSpeed").floatValue = 0f;
        so.FindProperty("fireInterval").floatValue = 3.4f;
        so.FindProperty("attackRange").floatValue = 42f;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<Rigidbody>().isKinematic = true;
        return go.GetComponent<Enemy>();
    }

    // ================================================================== pieces
    private static Transform Group(string name) => Graybox.Group(root, name);

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat, bool wallRun = false, float yaw = 0f)
        => Graybox.Box(parent, name, center, size, mat, wallRun, yaw);

    /// <summary>A floating island: grass top at y = basePoint.y, dirt cliff below.</summary>
    private static void Island(Transform parent, Vector3 center, Vector2 size, string name = "Island")
    {
        var g = Graybox.Group(parent, name);
        Box(g, "Grass", center + Vector3.down * 0.25f, new Vector3(size.x, 0.5f, size.y), groundMat);
        Box(g, "Cliff", center + Vector3.down * 4.5f, new Vector3(size.x * 0.96f, 8f, size.y * 0.96f), cliffMat);
    }

    /// <summary>A tall wall-run wall standing in a pit (runs along Z when yaw = 0).</summary>
    private static void WallRunWall(Transform parent, Vector3 basePoint, float length, float yaw)
    {
        Box(parent, "WallRunWall", basePoint + new Vector3(0f, 1.5f, 0f), new Vector3(0.6f, 11f, length), wallRunMat, true, yaw);
    }

    /// <summary>A free-standing wall-run slab on solid ground (traversal + cover).</summary>
    private static void FreeWall(Transform parent, Vector3 basePoint, float length, float yaw)
    {
        Box(parent, "WallRunSlab", basePoint + new Vector3(0f, 3f, 0f), new Vector3(0.6f, 6f, length), wallRunMat, true, yaw);
    }

    private static void Terrace(Transform parent, Vector3 basePoint, Vector3 size)
    {
        Box(parent, "Terrace", basePoint + Vector3.up * (size.y * 0.5f), size, groundMat);
    }

    private static void Rock(Transform parent, Vector3 basePoint, Vector3 size, float yaw)
    {
        Box(parent, "Rock", basePoint + Vector3.up * (size.y * 0.5f), size, rockMat, false, yaw);
    }

    /// <summary>
    /// Slim square tower that houses one caster: a wall-runnable shaft, an open deck (no parapet, so
    /// nothing blocks shots either way), four thin corner posts and a roof. A spiral of wall-runnable
    /// step pillars climbs to the deck. Returns the caster.
    /// </summary>
    private static Enemy Tower(Transform parent, Vector3 basePoint, float height, float stepsYaw = 0f)
    {
        var t = Graybox.Group(parent, "Tower");
        const float shaft = 2.5f, deck = 4f;
        Box(t, "Shaft", basePoint + Vector3.up * (height * 0.5f), new Vector3(shaft, height, shaft), wallRunMat, true);
        Box(t, "Deck", basePoint + Vector3.up * (height + 0.2f), new Vector3(deck, 0.4f, deck), stoneMat);
        float top = height + 0.4f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * (deck * 0.5f - 0.15f) * 1.414f;
            Box(t, "Post", basePoint + corner + Vector3.up * (top + 1.6f), new Vector3(0.25f, 3.2f, 0.25f), stoneMat);
        }
        Box(t, "Roof", basePoint + Vector3.up * (top + 3.4f), new Vector3(deck + 0.6f, 0.4f, deck + 0.6f), roofMat);
        // Ramp from the ground up to the deck edge, pointing along stepsYaw.
        Vector3 dir = Quaternion.Euler(0f, stepsYaw, 0f) * Vector3.forward;
        Ramp(t, basePoint + dir * (deck * 0.5f) + Vector3.up * top, dir, 2.6f, 32f);
        return Garrison(t, basePoint + Vector3.up * top);
    }

    /// <summary>A walkable slope from the ground (y = 0) up to <paramref name="top"/>, running out along dir.</summary>
    private static void Ramp(Transform parent, Vector3 top, Vector3 dir, float width, float angle)
    {
        float h = top.y;
        float rad = angle * Mathf.Deg2Rad;
        float run = h / Mathf.Tan(rad), length = h / Mathf.Sin(rad);
        const float thick = 0.4f;
        var rot = Quaternion.LookRotation(dir) * Quaternion.Euler(angle, 0f, 0f);
        Vector3 center = top + dir * (run * 0.5f) + Vector3.down * (h * 0.5f) + rot * Vector3.down * (thick * 0.5f);
        var go = Box(parent, "Ramp", center, new Vector3(width, thick, length), stoneMat);
        go.transform.rotation = rot;
    }

    /// <summary>
    /// A small stone shelter to duck into: back wall, two side walls and a roof, open at the front
    /// (the front faces along yaw). Blocks enemy bolts while you wait out a cooldown.
    /// </summary>
    public static void Cubby(Transform parent, Vector3 basePoint, float yaw, Material mat)
    {
        var c = Graybox.Group(parent, "Cubby");
        c.position = basePoint;
        c.rotation = Quaternion.Euler(0f, yaw, 0f);
        const float w = 3.6f, d = 2.6f, h = 2.6f, t = 0.4f;
        CubbyPart(c, "Back", new Vector3(0f, h * 0.5f, -d * 0.5f), new Vector3(w, h, t), mat);
        CubbyPart(c, "Side", new Vector3(-w * 0.5f + t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), mat);
        CubbyPart(c, "Side", new Vector3(w * 0.5f - t * 0.5f, h * 0.5f, 0f), new Vector3(t, h, d), mat);
        CubbyPart(c, "Roof", new Vector3(0f, h + t * 0.5f, 0f), new Vector3(w + 0.3f, t, d + 0.3f), mat);
    }

    private static void CubbyPart(Transform c, string name, Vector3 local, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(c, false);
        go.transform.localPosition = local;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    /// <summary>
    /// Spiral of wall-runnable pillars around a tower, each about 2 m higher than the last (a double
    /// jump clears it), with the last one level with the deck.
    /// </summary>
    private static void ClimbSteps(Transform parent, Vector3 basePoint, float deckTop, float radius, float startYaw)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(deckTop / 2f));
        float rise = deckTop / count;
        for (int i = 0; i < count; i++)
        {
            float h = rise * (i + 1);
            Vector3 dir = Quaternion.Euler(0f, startYaw + i * 70f, 0f) * Vector3.forward;
            Box(parent, $"Step_{i + 1}", basePoint + dir * radius + Vector3.up * (h * 0.5f), new Vector3(1.4f, h, 1.4f), wallRunMat, true, startYaw + i * 70f);
        }
    }

    /// <summary>
    /// Slide tunnel: two side walls and a roof 1.3 m up. Too low to walk through: hold slide (Ctrl).
    /// Runs along Z at yaw 0. The side walls are wall-runnable on the outside.
    /// </summary>
    private static void SlideTunnel(Transform parent, Vector3 basePoint, float length, float width, float yaw)
    {
        var g = Graybox.Group(parent, "SlideTunnel");
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        const float clearance = 1.3f, roof = 0.8f;
        Box(g, "Roof", basePoint + Vector3.up * (clearance + roof * 0.5f), new Vector3(width + 1.2f, roof, length), stoneMat, false, yaw);
        Box(g, "Side_L", basePoint + r * new Vector3(-(width * 0.5f + 0.3f), 1.5f, 0f), new Vector3(0.6f, 3f, length), wallRunMat, true, yaw);
        Box(g, "Side_R", basePoint + r * new Vector3(width * 0.5f + 0.3f, 1.5f, 0f), new Vector3(0.6f, 3f, length), wallRunMat, true, yaw);
        // Glowing strip on the entry edges so the low gap reads from a distance.
        Box(g, "Edge_In", basePoint + r * new Vector3(0f, clearance + 0.05f, -length * 0.5f), new Vector3(width, 0.1f, 0.12f), portalMat, false, yaw);
        Box(g, "Edge_Out", basePoint + r * new Vector3(0f, clearance + 0.05f, length * 0.5f), new Vector3(width, 0.1f, 0.12f), portalMat, false, yaw);
    }

    /// <summary>A fallen log / low beam to slide under, raised on two stumps. Also cover.</summary>
    private static void LowBeam(Transform parent, Vector3 basePoint, float width, float yaw, Material mat = null)
    {
        var rockMat = mat != null ? mat : LevelBuilder.rockMat;
        var g = Graybox.Group(parent, "LowBeam");
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Box(g, "Beam", basePoint + Vector3.up * 1.75f, new Vector3(width, 0.9f, 1.2f), rockMat, false, yaw);
        Box(g, "Stump_L", basePoint + r * new Vector3(-width * 0.5f + 0.4f, 0.65f, 0f), new Vector3(0.8f, 1.3f, 1.2f), rockMat, false, yaw);
        Box(g, "Stump_R", basePoint + r * new Vector3(width * 0.5f - 0.4f, 0.65f, 0f), new Vector3(0.8f, 1.3f, 1.2f), rockMat, false, yaw);
    }

    /// <summary>Two parallel wall-run slabs (a corridor to wall-run / wall-jump through). Runs along Z at yaw 0.</summary>
    private static void WallRunCorridor(Transform parent, Vector3 center, float length, float gap, float height, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Box(parent, "CorridorWall", center + r * new Vector3(-gap * 0.5f, height * 0.5f, 0f), new Vector3(0.6f, height, length), wallRunMat, true, yaw);
        Box(parent, "CorridorWall", center + r * new Vector3(gap * 0.5f, height * 0.5f, 0f), new Vector3(0.6f, height, length), wallRunMat, true, yaw);
    }

    /// <summary>Solid round tower (cylinder with a matching collider) to wall-run around.</summary>
    private static GameObject RoundTower(Transform parent, Vector3 basePoint, float radius, float height, bool merlons = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "RoundTower";
        go.transform.SetParent(parent, false);
        go.transform.position = basePoint + Vector3.up * (height * 0.5f);
        go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        Object.DestroyImmediate(go.GetComponent<CapsuleCollider>());   // capsule ends are rounded; use the real shape
        go.AddComponent<MeshCollider>();
        go.GetComponent<MeshRenderer>().sharedMaterial = wallRunMat;
        go.tag = Graybox.WallRunTag;
        go.isStatic = true;
        // Crenellated cap ring so it reads as a castle tower.
        for (int i = 0; i < (merlons ? 8 : 0); i++)
        {
            float yaw = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Box(go.transform.parent, "Merlon", basePoint + dir * (radius - 0.3f) + Vector3.up * (height + 0.5f), new Vector3(radius * 0.6f, 1f, 0.6f), stoneMat, false, yaw);
        }
        return go;
    }

    /// <summary>Round tower with a deck and roof on top, housing one caster. Returns the caster.</summary>
    private static Enemy WatchTower(Transform parent, Vector3 basePoint, float radius, float height)
    {
        var t = Graybox.Group(parent, "WatchTower");
        RoundTower(t, basePoint, radius, height, merlons: false);   // open deck: nothing blocks shots
        float deck = radius * 2f + 0.6f;
        Box(t, "Deck", basePoint + Vector3.up * (height + 0.2f), new Vector3(deck, 0.4f, deck), stoneMat);
        float top = height + 0.4f;
        for (int i = 0; i < 4; i++)
        {
            Vector3 corner = Quaternion.Euler(0f, 45f + i * 90f, 0f) * Vector3.forward * (deck * 0.5f - 0.2f) * 1.414f;
            Box(t, "Post", basePoint + corner + Vector3.up * (top + 1.6f), new Vector3(0.25f, 3.2f, 0.25f), stoneMat);
        }
        Box(t, "Roof", basePoint + Vector3.up * (top + 3.4f), new Vector3(deck + 0.6f, 0.4f, deck + 0.6f), roofMat);
        return Garrison(t, basePoint + Vector3.up * top);
    }

    /// <summary>Wall-runnable slab from one point to another (links towers so you can run tower to tower).</summary>
    private static void ConnectWall(Transform parent, Vector3 a, Vector3 b, float height)
    {
        Vector3 d = b - a; d.y = 0f;
        float yaw = Quaternion.LookRotation(d).eulerAngles.y;
        Box(parent, "LinkWall", (a + b) * 0.5f + Vector3.up * (height * 0.5f), new Vector3(0.6f, height, d.magnitude), wallRunMat, true, yaw);
    }

    /// <summary>Wooden barricade: chest-high wall of planks with two braces. Enemies hide behind these.</summary>
    private static void Barricade(Transform parent, Vector3 basePoint, float yaw)
    {
        var b = Graybox.Group(parent, "Barricade");
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Box(b, "Planks", basePoint + Vector3.up * 0.75f, new Vector3(4f, 1.5f, 0.35f), woodMat, false, yaw);
        Box(b, "Brace_L", basePoint + r * new Vector3(-1.4f, 0.5f, -0.6f), new Vector3(0.25f, 1f, 1.2f), woodMat, false, yaw);
        Box(b, "Brace_R", basePoint + r * new Vector3(1.4f, 0.5f, -0.6f), new Vector3(0.25f, 1f, 1.2f), woodMat, false, yaw);
    }

    /// <summary>Several barricades in a row with walk gaps between them.</summary>
    private static void BarricadeLine(Transform parent, Vector3 center, int count, float yaw)
    {
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        float spacing = 6f;
        for (int i = 0; i < count; i++)
        {
            float offset = (i - (count - 1) * 0.5f) * spacing;
            Barricade(parent, center + r * new Vector3(offset, 0f, 0f), yaw);
        }
    }

    /// <summary>Thick curtain wall (wall-runnable). Runs along X at yaw 0.</summary>
    private static void CastleWall(Transform parent, Vector3 basePoint, float length, float yaw, float height)
    {
        Box(parent, "CastleWall", basePoint + Vector3.up * (height * 0.5f), new Vector3(length, height, 3f), stoneMat, true, yaw);
    }

    /// <summary>A wall across the grounds at z with a central gate flanked by two big round towers.</summary>
    private static GameObject GatedWall(Transform parent, float z, float halfWidth, float gateWidth, float height)
    {
        float segment = halfWidth - gateWidth * 0.5f;
        CastleWall(parent, new Vector3(-(gateWidth * 0.5f + segment * 0.5f), 0f, z), segment, 0f, height);
        CastleWall(parent, new Vector3(gateWidth * 0.5f + segment * 0.5f, 0f, z), segment, 0f, height);
        RoundTower(parent, new Vector3(-gateWidth * 0.5f - 2.5f, 0f, z), 3.5f, height + 4f);
        RoundTower(parent, new Vector3(gateWidth * 0.5f + 2.5f, 0f, z), 3.5f, height + 4f);
        return BuildGateDoor(parent, z, gateWidth, height, woodMat);
    }

    /// <summary>A giant two-leaf wooden gate filling a gateway at z (centred on x = 0). Swings open via GateDoor.</summary>
    public static GameObject BuildGateDoor(Transform parent, float z, float width, float height, Material wood)
    {
        var root = Graybox.Group(parent, "GateDoor");
        var door = root.gameObject.AddComponent<GateDoor>();
        var iron = Mat("L2_GateIron", new Color(0.18f, 0.18f, 0.2f));
        float leaf = width * 0.5f;
        for (int side = -1; side <= 1; side += 2)
        {
            var hinge = Graybox.Group(root, side < 0 ? "LeftHinge" : "RightHinge");
            hinge.position = new Vector3(side * leaf, 0f, z);
            float cx = side * leaf * 0.5f;   // leaf centre, toward the middle
            Box(hinge, "Leaf", new Vector3(cx, height * 0.5f, z), new Vector3(leaf - 0.05f, height, 0.8f), wood);
            for (int b = 0; b < 3; b++)
            {
                float y = height * (0.2f + b * 0.3f);
                Box(hinge, "Band", new Vector3(cx, y, z - 0.45f), new Vector3(leaf - 0.1f, 0.35f, 0.1f), iron);
                Box(hinge, "Band", new Vector3(cx, y, z + 0.45f), new Vector3(leaf - 0.1f, 0.35f, 0.1f), iron);
            }
            if (side < 0) door.leftHinge = hinge; else door.rightHinge = hinge;
        }
        foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;   // it moves
        return root.gameObject;
    }

    /// <summary>
    /// Exit portal: frame, glowing surface (trigger with SceneExit) and a tall light beam so it can be
    /// found from anywhere. Starts inactive; LevelManager opens it when the level is cleared.
    /// </summary>
    private static GameObject Portal(Transform parent, Vector3 basePoint, float yaw)
    {
        var p = Graybox.Group(parent, "ExitPortal");
        p.position = basePoint;
        p.rotation = Quaternion.Euler(0f, yaw, 0f);
        Quaternion r = p.rotation;
        Box(p, "Frame_L", basePoint + r * new Vector3(-2.25f, 2.25f, 0f), new Vector3(0.6f, 4.5f, 0.6f), stoneMat, false, yaw);
        Box(p, "Frame_R", basePoint + r * new Vector3(2.25f, 2.25f, 0f), new Vector3(0.6f, 4.5f, 0.6f), stoneMat, false, yaw);
        Box(p, "Frame_Top", basePoint + r * new Vector3(0f, 4.75f, 0f), new Vector3(5.1f, 0.6f, 0.6f), stoneMat, false, yaw);
        var surface = Box(p, "Surface", basePoint + r * new Vector3(0f, 2.25f, 0f), new Vector3(3.9f, 4.5f, 0.2f), portalMat, false, yaw);
        surface.isStatic = false;
        var col = surface.GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 10f);   // deep trigger so you can't miss it
        var exit = surface.AddComponent<SceneExit>();
        var so = new SerializedObject(exit);
        so.FindProperty("nextScene").stringValue = "";   // GameFlow picks the next campaign scene
        so.ApplyModifiedPropertiesWithoutUndo();

        // Swirling Hovl portal effect on the surface.
        var portalFx = VfxSetup.PortalEffect();
        if (portalFx != null)
        {
            var fx = (GameObject)PrefabUtility.InstantiatePrefab(portalFx, p);
            fx.transform.position = basePoint + r * new Vector3(0f, 2.25f, 0f);
            fx.transform.rotation = r;
            fx.transform.localScale = Vector3.one * 2.2f;
            surface.GetComponent<MeshRenderer>().enabled = false;   // the effect is the surface now
        }

        var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = "Beacon";
        Object.DestroyImmediate(beam.GetComponent<Collider>());
        beam.transform.SetParent(p, false);
        beam.transform.position = basePoint + Vector3.up * 40f;
        beam.transform.localScale = new Vector3(0.8f, 40f, 0.8f);
        beam.GetComponent<MeshRenderer>().sharedMaterial = BeaconMaterial();

        var light = new GameObject("PortalLight").AddComponent<Light>();
        light.transform.SetParent(p, false);
        light.transform.position = basePoint + Vector3.up * 2.5f;
        light.type = LightType.Point;
        light.color = new Color(0.4f, 0.9f, 1f);
        light.range = 12f;
        light.intensity = 3f;
        return p.gameObject;
    }

    private static Material BeaconMaterial()
    {
        string path = MatDir + "PortalBeacon.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = new Color(0.45f, 0.9f, 1f, 0.35f);
        EditorUtility.SetDirty(mat);
        return mat;
    }
}
