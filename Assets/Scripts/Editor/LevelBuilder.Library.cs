using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Build Level 3 (Library). The final level: one huge enclosed library.
///   Area 1 "Ground Floor": spawn at the entrance. Reading tables down the middle, bookcase stacks to wall-run
///          on either side (every bookcase is wall-runnable), four floating mages hovering over the stacks,
///          and the usual waves. Clearing it lowers a spiral staircase from the floor above into the rotunda.
///   Area 2 "Upper Hall": see LevelBuilder.Library2.cs (two storeys, grand staircase, wrecked, giant metal door).
///   Area 3 "Sealed Door": the corridor behind the metal door (placeholder for the boss room) with the exit portal.
/// Layout: hall x -32..32, z 0..120. Ground floor y 0..12, ceiling slab 12..13 with a 10 m hole over the
/// rotunda at z 64, upper floor 13..28.
/// </summary>
public static partial class LevelBuilder
{
    private static Material shelfMat, shelfWoodMat, ironMat, rugMat, flameMat;
    private const float LibHalfW = 32f, LibLength = 120f, FloorTop = 13f, Roof = 28f, RotundaZ = 64f, HoleHalf = 5f;
    private const string Dungeon = "Assets/LowPolyDungeonsLite/Prefabs/";
    private const string Olive = "Assets/Blue Olive Studio/Magic Spellbook & Book Stand – Wizard Props/Built-In/";
    private const string RpgProps = "Assets/RPGPP_LT/Prefabs/Props/";

    [MenuItem("Oxford Runner/Build Level 3 (Library)")]
    public static void BuildLevel3()
    {
        if (!Begin("Level3", "Library")) return;
        groundMat = Mat("L3_FloorWood", new Color(0.55f, 0.38f, 0.24f));
        stoneMat = Mat("L3_WallStone", new Color(0.5f, 0.46f, 0.42f));
        shelfMat = Mat("L3_Bookshelf", Color.white);
        shelfWoodMat = Mat("L3_ShelfWood", new Color(0.36f, 0.22f, 0.12f));
        woodMat = shelfWoodMat;
        roofMat = Mat("L3_CeilingWood", new Color(0.3f, 0.2f, 0.13f));
        ironMat = Mat("L3_Iron", new Color(0.16f, 0.15f, 0.15f));
        rugMat = Mat("L3_Rug", new Color(0.45f, 0.08f, 0.1f));
        portalMat = Mat("Portal", new Color(0.4f, 0.9f, 1f));
        flameMat = Mat("L3_Flame", new Color(1f, 0.7f, 0.3f));
        flameMat.EnableKeyword("_EMISSION");
        flameMat.SetColor("_EmissionColor", new Color(1f, 0.6f, 0.2f) * 2f);
        flameMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(flameMat);
        AssetDatabase.SaveAssetIfDirty(flameMat);   // or the emission keyword is lost
        Indoors();

        var shell = Group("Shell");
        LibraryShell(shell);
        var a1 = Group("A_GroundFloor");
        WallBookcases(a1);
        Entrance(a1);
        ReadingArea(a1);
        Stacks(a1);
        Chandeliers(a1);
        ScatteredBooks(a1);

        // Floating mages low (6.5 m: higher up they hugged the 12 m ceiling) over the middle of the hall, not the corners; waves spawning around the rotunda in the
        // middle: nothing comes from the far end or out of the side stacks, where walkers got stuck.
        var garrison = new List<Enemy>
        {
            FloatingMage(a1, new Vector3(-7f, 6.5f, 46f), wander: 5f),
            FloatingMage(a1, new Vector3(7f, 6.5f, 50f), wander: 5f),
            FloatingMage(a1, new Vector3(-7f, 6.5f, 82f), wander: 5f),
            FloatingMage(a1, new Vector3(7f, 6.5f, 78f), wander: 5f),
        };
        Encounter("Ground Floor", a1, new Vector3(0f, 0f, 66f), new Vector2(LibHalfW * 2f, 108f), garrison,
                  new[] { new Vector3(-18f, 0f, 62f), new Vector3(18f, 0f, 66f), new Vector3(-6f, 0f, 52f), new Vector3(6f, 0f, 52f),
                          new Vector3(-6f, 0f, 76f), new Vector3(6f, 0f, 76f) },
                  Wave(8, 60f, 4.2f, 2.4f), Wave(8, 65f, 4.2f, 2.2f));   // 16: still 2 commanders (auto)

        // Area 2: the staircase comes down when the ground floor is cleared; a hatch shuts over it once you're up.
        var a2 = Group("B_UpperHall");
        var stairs = SpiralStaircase(a2, new Vector3(0f, 0f, RotundaZ));
        UpperHall(a1, a2, stairs);

        Finish("Level 3 - The Library", new Vector3(0f, 0f, 4f), 0f, null, killY: -20f, killCenterZ: 60f, runeGlow: 0.5f);   // dim: the library is dark, full glow is glaring
    }

    // ------------------------------------------------------------------ lighting
    private static void Indoors()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.12f, 0.08f, 0.05f);
        RenderSettings.fogDensity = 0.012f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.3f, 0.24f, 0.19f);
        var sun = GameObject.Find("Directional Light");
        if (sun != null) Object.DestroyImmediate(sun);   // enclosed: chandeliers and torches only
    }

    private static Light PointLight(Transform parent, string name, Vector3 pos, Color color, float range, float intensity, bool important, float flicker)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
        l.renderMode = important ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
        if (flicker > 0f) go.AddComponent<LightFlicker>().amount = flicker;
        return l;
    }

    private static readonly Color Candlelight = new Color(1f, 0.75f, 0.45f);
    private static readonly Color Torchlight = new Color(1f, 0.55f, 0.25f);

    // ------------------------------------------------------------------ shell
    // Boxes are tiled (not one giant box) so each piece picks up its nearest lights.
    private static void Slab(Transform parent, string name, float x0, float x1, float z0, float z1, float y, float thick, Material mat, float tile = 16f)
    {
        int nx = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / tile)), nz = Mathf.Max(1, Mathf.CeilToInt((z1 - z0) / tile));
        float sx = (x1 - x0) / nx, sz = (z1 - z0) / nz;
        for (int i = 0; i < nx; i++)
            for (int j = 0; j < nz; j++)
                Box(parent, name, new Vector3(x0 + sx * (i + 0.5f), y - thick * 0.5f, z0 + sz * (j + 0.5f)), new Vector3(sx, thick, sz), mat);
    }

    private static void LibraryShell(Transform g)
    {
        Slab(g, "Floor", -LibHalfW, LibHalfW, 0f, LibLength, 0f, 1f, groundMat);
        // Ceiling of the ground floor = floor of the upper floor, with a square hole over the rotunda.
        Slab(g, "UpperFloor", -LibHalfW, LibHalfW, 0f, RotundaZ - HoleHalf, FloorTop, 1f, groundMat);
        Slab(g, "UpperFloor", -LibHalfW, LibHalfW, RotundaZ + HoleHalf, LibLength, FloorTop, 1f, groundMat);
        Slab(g, "UpperFloor", -LibHalfW, -HoleHalf, RotundaZ - HoleHalf, RotundaZ + HoleHalf, FloorTop, 1f, groundMat);
        Slab(g, "UpperFloor", HoleHalf, LibHalfW, RotundaZ - HoleHalf, RotundaZ + HoleHalf, FloorTop, 1f, groundMat);
        Slab(g, "Roof", -LibHalfW, LibHalfW, 0f, LibLength, Roof + 1f, 1f, roofMat, 24f);
        // Dark wooden beams under the ground floor ceiling.
        for (float z = 10f; z < LibLength; z += 12f)
            if (Mathf.Abs(z - RotundaZ) > HoleHalf + 1f)
                Box(g, "CeilingBeam", new Vector3(0f, FloorTop - 1.4f, z), new Vector3(LibHalfW * 2f, 0.8f, 0.7f), roofMat);
        // Outer walls, full height, in 20 m pieces.
        for (float z = 0f; z < LibLength; z += 20f)
            for (int s = -1; s <= 1; s += 2)
                Box(g, "Wall", new Vector3(s * (LibHalfW + 1f), Roof * 0.5f, z + 10f), new Vector3(2f, Roof + 2f, 20f), stoneMat);
        for (float x = -LibHalfW; x < LibHalfW; x += 16f)
        {
            Box(g, "Wall", new Vector3(x + 8f, Roof * 0.5f, -1f), new Vector3(16f, Roof + 2f, 2f), stoneMat);
        }
        // Back wall with the doorway to the boss room (x -4..4, from the balcony at y 19 up to 27).
        for (int s = -1; s <= 1; s += 2)
            for (int i = 0; i < 2; i++)
                Box(g, "Wall", new Vector3(s * (4f + 7f + i * 14f), Roof * 0.5f, LibLength + 1f), new Vector3(14f, Roof + 2f, 2f), stoneMat);
        Box(g, "Wall", new Vector3(0f, (BalconyY - 1f) * 0.5f, LibLength + 1f), new Vector3(8f, BalconyY + 1f, 2f), stoneMat);
        Box(g, "Wall", new Vector3(0f, (DoorTop + Roof + 1f) * 0.5f, LibLength + 1f), new Vector3(8f, Roof + 1f - DoorTop, 2f), stoneMat);
    }

    /// <summary>A bookcase (wall-runnable, books on both long faces) with a wooden cap and end boards. Runs along local X.</summary>
    private static Transform Bookcase(Transform parent, Vector3 basePoint, float length, float height, float yaw, float thick = 1f)
    {
        var b = Graybox.Group(parent, "Bookcase");
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Box(b, "Books", basePoint + Vector3.up * (height * 0.5f), new Vector3(length, height, thick), shelfMat, true, yaw);
        Box(b, "Cap", basePoint + Vector3.up * (height + 0.15f), new Vector3(length + 0.3f, 0.3f, thick + 0.3f), shelfWoodMat, false, yaw);
        for (int s = -1; s <= 1; s += 2)
            Box(b, "End", basePoint + r * new Vector3(s * (length * 0.5f + 0.08f), height * 0.5f, 0f), new Vector3(0.16f, height, thick + 0.2f), shelfWoodMat, false, yaw);
        return b;
    }

    // Floor-to-ceiling bookcases along both side walls, split by stone pilasters that carry the torches.
    private static void WallBookcases(Transform parent)
    {
        var g = Graybox.Group(parent, "WallBookcases");
        const float h = FloorTop - 1.8f;   // up to the ceiling beams
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (LibHalfW - 0.5f);
            float z = 2f;
            while (z + 11.6f <= LibLength - 1f)
            {
                Pilaster(g, new Vector3(s * (LibHalfW - 0.7f), 0f, z), s);
                Bookcase(g, new Vector3(x, 0f, z + 5.8f), 10f, h, 90f);
                z += 11.6f;
            }
            Pilaster(g, new Vector3(s * (LibHalfW - 0.7f), 0f, z), s);
        }
        // Back wall: bookcases with a banner in the middle.
        for (int s = -1; s <= 1; s += 2)
        {
            Bookcase(g, new Vector3(s * 9f, 0f, LibLength - 0.5f), 14f, h, 0f);
            Bookcase(g, new Vector3(s * 24f, 0f, LibLength - 0.5f), 14f, h, 0f);
        }
        Prop(g, Dungeon + "WallDecor_05.prefab", new Vector3(0f, 4.5f, LibLength - 0.15f), 180f, 2.4f);
    }

    // Stone pilaster against a side wall with a torch facing into the hall (side = -1 left wall, 1 right wall).
    private static void Pilaster(Transform parent, Vector3 basePoint, int side)
    {
        Box(parent, "Pilaster", basePoint + Vector3.up * (FloorTop * 0.5f), new Vector3(1.4f, FloorTop, 1.6f), stoneMat);
        WallTorch(parent, basePoint + new Vector3(-side * 0.75f, 4.2f, 0f), side > 0 ? -90f : 90f);
    }

    private static void WallTorch(Transform parent, Vector3 pos, float yaw)
    {
        Prop(parent, Dungeon + "Light_08.prefab", pos, yaw, 2f);
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        PointLight(parent, "TorchLight", pos + Vector3.up * 1.3f + forward * 0.4f, Torchlight, 10f, 1.5f, false, 0.25f);
    }

    // ------------------------------------------------------------------ ground floor
    private static void Entrance(Transform parent)
    {
        var g = Graybox.Group(parent, "Entrance");
        BuildGateDoor(g, 0.4f, 8f, 7f, shelfWoodMat);   // the doors you came in through (stay shut)
        Box(g, "DoorFrame", new Vector3(0f, 7.4f, 0.3f), new Vector3(9.6f, 0.8f, 1f), stoneMat);
        for (int s = -1; s <= 1; s += 2)
        {
            Box(g, "DoorFrame", new Vector3(s * 4.4f, 3.5f, 0.3f), new Vector3(0.8f, 7f, 1f), stoneMat);
            // Front wall bookcases either side of the doors.
            Bookcase(g, new Vector3(s * 11f, 0f, 0.5f), 12f, FloorTop - 1.8f, 0f);
            Bookcase(g, new Vector3(s * 24.5f, 0f, 0.5f), 13f, FloorTop - 1.8f, 0f);
            WallTorch(g, new Vector3(s * 5.3f, 4.2f, 0.4f), 0f);
            Prop(g, Olive + "Floor Candle Holder 1.prefab", new Vector3(s * 5.4f, 0f, 1.6f), 0f, 1.2f);   // beside the doors, out of the spawn view
        }
        // Red runner down the middle of the hall.
        Box(g, "Runner", new Vector3(0f, 0.01f, 60f), new Vector3(3f, 0.02f, 112f), rugMat);
        StripLights(g);
    }

    // Reading tables with benches, candles and books down the middle; a rotunda (where the stairs land) in the centre.
    private static void ReadingArea(Transform parent)
    {
        var g = Graybox.Group(parent, "ReadingArea");
        var rng = new System.Random(3303);
        foreach (float z in new[] { 22f, 32f, 42f, 86f, 96f, 106f })
            foreach (float x in new[] { -6.5f, 6.5f })
            {
                Prop(g, RpgProps + "Tables/rpgpp_lt_table_01.prefab", new Vector3(x, 0f, z), 0f, 1.3f, keepColliders: true);
                Prop(g, RpgProps + "Benches/rpgpp_lt_bench_wood_01.prefab", new Vector3(x, 0f, z - 1.6f), 0f, 1.6f);
                Prop(g, RpgProps + "Benches/rpgpp_lt_bench_wood_01.prefab", new Vector3(x, 0f, z + 1.6f), 180f, 1.6f);
                Prop(g, Olive + "Candles 1.prefab", new Vector3(x + 0.6f, 1.24f, z), (float)rng.NextDouble() * 360f, 1.2f);
                for (int b = 0; b < 2 + rng.Next(3); b++)
                    Prop(g, Dungeon + "Book_09.prefab", new Vector3(x - 0.7f + (float)rng.NextDouble() * 0.3f, 1.24f + b * 0.14f, z - 0.1f), (float)rng.NextDouble() * 360f, 2f);
            }
        // Candle stands along the reading area edges.
        foreach (float z in new[] { 27f, 37f, 91f, 101f })
            for (int s = -1; s <= 1; s += 2)
                Prop(g, Olive + "Floor Candle Holder 1.prefab", new Vector3(s * 10f, 0f, z), 0f, 1.3f);

        // Rotunda: round rug, ring of candle stands, lecterns with open books. The centre stays clear for the stairs.
        var rug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rug.name = "RotundaRug";
        rug.transform.SetParent(g, false);
        rug.transform.position = new Vector3(0f, 0.015f, RotundaZ);
        rug.transform.localScale = new Vector3(15f, 0.01f, 15f);
        Object.DestroyImmediate(rug.GetComponent<Collider>());
        rug.GetComponent<Renderer>().sharedMaterial = rugMat;
        rug.isStatic = true;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f + 22.5f;
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Prop(g, Olive + "Floor Candle Holder 1.prefab", new Vector3(0f, 0f, RotundaZ) + dir * 7f, a, 1.3f);
            if (i % 2 == 0) Prop(g, Olive + "Magic Book-Stand 1.prefab", new Vector3(0f, 0f, RotundaZ) + dir * 9.5f, a + 180f, 1.6f, keepColliders: true);
        }
        StripLights(g);
        PointLight(g, "RotundaLight", new Vector3(0f, 9f, RotundaZ), Candlelight, 16f, 1.2f, false, 0f);
    }

    // Wall-running sides: a long bookcase wall on each side of the reading area (with a slide-under gap),
    // then bookcase stacks across to the side walls, alternating heights, with an outer aisle along the wall.
    private static void Stacks(Transform parent)
    {
        var g = Graybox.Group(parent, "Stacks");
        for (int s = -1; s <= 1; s += 2)
        {
            float inner = s * 12.5f;
            foreach (var seg in new[] { new Vector2(14f, 34f), new Vector2(38f, 56f), new Vector2(72f, 90f), new Vector2(94f, 114f) })
                Bookcase(g, new Vector3(inner, 0f, (seg.x + seg.y) * 0.5f), seg.y - seg.x, 7f, 90f);
            // Bookcases lying across the gaps, 1.3 m up: slide under them.
            foreach (float z in new[] { 36f, 92f })
            {
                Box(g, "SlideShelf", new Vector3(inner, 1.3f + 0.6f, z), new Vector3(1f, 1.2f, 4.4f), shelfMat, false);
                Box(g, "SlideShelfCap", new Vector3(inner, 3.2f, z), new Vector3(1.3f, 0.3f, 4.4f), shelfWoodMat);
                for (int e = -1; e <= 1; e += 2)
                    Box(g, "SlidePost", new Vector3(inner, 0.65f, z + e * 1.9f), new Vector3(1f, 1.3f, 0.3f), shelfWoodMat);
            }

            int n = 0;
            foreach (float z in new[] { 18f, 26f, 34f, 42f, 50f, 78f, 86f, 94f, 102f, 110f })
                Bookcase(g, new Vector3(s * 21.75f, 0f, z), 10.5f, n++ % 2 == 0 ? 6f : 8f, 0f);

            // Fallen bookcases across the outer aisle, propped 1.3 m up: slide under.
            foreach (float z in new[] { 46f, 98f })
                LowBeam(g, new Vector3(s * 29.2f, 0f, z), 4.6f, 0f, shelfWoodMat);
            // Rolling ladders against the wall bookcases.
            foreach (float z in new[] { 22f, 58f, 82f })
            {
                var ladder = Prop(g, RpgProps + "Items/rpgpp_lt_ladder_01.prefab", new Vector3(s * 30.4f, 0f, z), s > 0 ? -90f : 90f, 2.2f);
                if (ladder != null) ladder.transform.rotation = Quaternion.Euler(0f, s > 0 ? -90f : 90f, 0f) * Quaternion.Euler(-12f, 0f, 0f);
            }
        }
    }

    // Books knocked off the shelves all over the floor (battle aftermath). Decorative, no colliders.
    private static void ScatteredBooks(Transform parent)
    {
        var g = Graybox.Group(parent, "ScatteredBooks");
        var rng = new System.Random(777);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        string[] kinds = { Dungeon + "Book_09.prefab", Olive + "Book-closed 10.prefab", Olive + "Book-closed 12.prefab", Olive + "Book-closed 14.prefab", Olive + "Book-open 2.prefab" };
        // Piles: books dumped around a point (where a shelf came down), plus loose ones everywhere.
        var piles = new List<Vector3>();
        for (int i = 0; i < 25; i++) piles.Add(new Vector3(R(-30f, 30f), 0f, R(12f, 116f)));
        for (int i = 0; i < 380; i++)   // kept light: more reads as clutter
        {
            var p = i < 220 ? piles[i % piles.Count] + new Vector3(R(-1.6f, 1.6f), 0f, R(-1.6f, 1.6f)) : new Vector3(R(-30f, 30f), 0f, R(10f, 117f));
            p.y = 0.02f + (i < 220 ? R(0f, 0.25f) : 0f);
            if (new Vector2(p.x, p.z - RotundaZ).magnitude < 5.5f) continue;   // stairs land here
            string k = kinds[rng.Next(kinds.Length)];
            var b = Prop(g, k, p, R(0f, 360f), k.Contains("Book_09") ? R(2.6f, 3.4f) : R(1.8f, 2.4f));
            if (b != null && rng.NextDouble() < 0.5) b.transform.rotation *= Quaternion.Euler(R(-30f, 30f), 0f, R(-35f, 35f));   // some lying askew
        }
        MergeByMaterial(g, 30f);
    }

    /// <summary>
    /// Replaces every mesh under g with one combined mesh per (material, z band of bandLength metres), so
    /// hundreds of small props become a handful of objects. Combined meshes are serialized into the scene.
    /// </summary>
    private static void MergeByMaterial(Transform g, float bandLength)
    {
        var groups = new Dictionary<(Material, int), List<CombineInstance>>();
        var lowDetail = new HashSet<Renderer>();   // only LOD0 of LOD'd props, or every LOD level would draw on top of each other
        foreach (var lg in g.GetComponentsInChildren<LODGroup>())
        {
            var lods = lg.GetLODs();
            for (int i = 1; i < lods.Length; i++) foreach (var r in lods[i].renderers) if (r != null) lowDetail.Add(r);
        }
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
        {
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null || mf.sharedMesh == null || lowDetail.Contains(mr)) continue;
            int band = Mathf.FloorToInt(mf.transform.position.z / bandLength);
            var mats = mr.sharedMaterials;
            for (int sub = 0; sub < mf.sharedMesh.subMeshCount && sub < mats.Length; sub++)
            {
                var key = (mats[sub], band);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, subMeshIndex = sub, transform = mf.transform.localToWorldMatrix });
            }
        }
        for (int i = g.childCount - 1; i >= 0; i--) Object.DestroyImmediate(g.GetChild(i).gameObject);
        foreach (var kv in groups)
        {
            var mesh = new Mesh { name = $"{g.name}_{kv.Key.Item1.name}_{kv.Key.Item2}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(g, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = kv.Key.Item1;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // flat on the floor; no shadow passes for them
            go.isStatic = true;
        }
    }

    private static void Chandeliers(Transform parent)
    {
        var g = Graybox.Group(parent, "Chandeliers");
        foreach (float z in new[] { 22f, 42f, 86f, 106f }) Chandelier(g, new Vector3(0f, 8.6f, z), FloorTop - 1f);
        Chandelier(g, new Vector3(-21f, 8.6f, RotundaZ), FloorTop - 1f);
        Chandelier(g, new Vector3(21f, 8.6f, RotundaZ), FloorTop - 1f);
    }

    /// <summary>Iron ring of candles on a chain from the ceiling, with one warm light.</summary>
    private static void Chandelier(Transform parent, Vector3 center, float ceilingY)
    {
        var c = Graybox.Group(parent, "Chandelier");
        Box(c, "Chain", new Vector3(center.x, (center.y + ceilingY) * 0.5f, center.z), new Vector3(0.08f, ceilingY - center.y, 0.08f), ironMat);
        const float radius = 1.6f;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f;
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            Box(c, "Ring", center + dir * radius * 0.924f, new Vector3(radius * 0.77f, 0.1f, 0.1f), ironMat, false, a + 90f);
            Box(c, "Spoke", center + dir * radius * 0.5f + Vector3.up * 0.25f, new Vector3(0.06f, 0.06f, radius), ironMat, false, a);
            Prop(c, Olive + "Candle 1.prefab", center + Quaternion.Euler(0f, a + 22.5f, 0f) * Vector3.forward * radius + Vector3.up * 0.05f, 0f, 1.4f);
        }
        Box(c, "Hub", center + Vector3.up * 0.4f, new Vector3(0.35f, 0.5f, 0.35f), ironMat);
        StripLights(c);
        PointLight(c, "ChandelierLight", center + Vector3.down * 0.3f, Candlelight, 26f, 2.1f, true, 0.06f);
        foreach (var col in c.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);   // never snags a wall-run
    }

    private static void StripLights(Transform t)
    {
        foreach (var l in t.GetComponentsInChildren<Light>())
            if (l.GetComponent<LightFlicker>() == null) Object.DestroyImmediate(l);   // keep our own torch / chandelier lights
    }

    /// <summary>A Mage that floats (kinematic) above a glowing circle, drifting around within wander metres, and
    /// shoots from there. 1 HP: one hit brings it down. hover = false: a stationary mage standing at pos (feet), also 1 HP.</summary>
    private static Enemy FloatingMage(Transform parent, Vector3 pos, bool hover = true, float wander = 5f)
    {
        var prefab = EnemySetup.Load(EnemySetup.MagePath);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, parent);
        go.name = "FloatingMage";
        go.transform.position = pos;
        var so = new SerializedObject(go.GetComponent<Enemy>());
        so.FindProperty("maxHealth").floatValue = 1f;
        so.FindProperty("moveSpeed").floatValue = 0f;
        so.FindProperty("fireInterval").floatValue = 2.8f;
        so.FindProperty("attackRange").floatValue = 48f;
        so.ApplyModifiedPropertiesWithoutUndo();
        go.GetComponent<Rigidbody>().isKinematic = true;
        if (!hover) { go.name = "BalconyMage"; go.transform.position = pos + Vector3.up * 1.02f; return go.GetComponent<Enemy>(); }
        go.AddComponent<Hover>().wander = wander;
        var circle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/Magic circles/Magic circle.prefab");
        if (circle != null)
        {
            var fx = (GameObject)PrefabUtility.InstantiatePrefab(circle, go.transform);
            fx.transform.localPosition = Vector3.down * 1.1f;
            fx.transform.localScale = Vector3.one * 0.5f;
            foreach (var c in fx.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        }
        return go.GetComponent<Enemy>();
    }

    // ------------------------------------------------------------------ stairs + upper floor
    /// <summary>
    /// Spiral staircase from the rotunda floor up through the ceiling hole to the upper floor. Built in place,
    /// then parked 13 m up (in the floor above); SlideShut lowers it when area 2 unlocks.
    /// The steps are visual only: a hidden ramp of tilted boxes through the step tops carries the player
    /// (the controller can't climb steps).
    /// </summary>
    private static GameObject SpiralStaircase(Transform parent, Vector3 basePoint)
    {
        var g = Graybox.Group(parent, "SpiralStaircase");
        g.position = basePoint;
        const int steps = 36;
        const float turn = 20f, rIn = 1f, rOut = 4.8f;
        float rise = FloorTop / steps, rMid = (rIn + rOut) * 0.5f;

        var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = "Column";
        column.transform.SetParent(g, false);
        column.transform.position = basePoint + Vector3.up * (FloorTop * 0.5f + 0.5f);
        column.transform.localScale = new Vector3(rIn * 2f, FloorTop * 0.5f + 0.5f, rIn * 2f);
        column.GetComponent<Renderer>().sharedMaterial = stoneMat;

        for (int i = 0; i < steps; i++)
        {
            float a = (i + 0.5f) * turn;
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            var step = Box(g, "Step", basePoint + dir * rMid + Vector3.up * ((i + 1) * rise - 0.12f), new Vector3(1.35f, 0.24f, rOut - rIn), shelfWoodMat, false, a);
            Object.DestroyImmediate(step.GetComponent<Collider>());
            var post = Box(g, "RailPost", basePoint + dir * (rOut - 0.1f) + Vector3.up * ((i + 1) * rise + 0.5f), new Vector3(0.08f, 1f, 0.08f), ironMat);
            Object.DestroyImmediate(post.GetComponent<Collider>());
        }
        // Hidden ramp: 4 short tilted boxes per step along the helix (i = -1 starts on the floor). Short pieces
        // sized to the OUTER edge leave no gaps or ledges there; long ones made the climb bumpy and let you slip off.
        const int sub = 4;
        float subTurn = turn / sub, subRise = rise / sub;
        float slope = Mathf.Atan2(subRise, rMid * subTurn * Mathf.Deg2Rad) * Mathf.Rad2Deg;
        float pieceLen = 2f * rOut * Mathf.Tan(subTurn * 0.5f * Mathf.Deg2Rad) + 0.1f;
        for (int k = 0; k < steps * sub; k++)   // from step -1's centre (on the floor) to the top step's centre
        {
            float a = (k + 0.5f) * subTurn - turn * 0.5f;
            float y = (k + 0.5f) * subRise - 0.1f;
            Vector3 dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
            var seg = new GameObject("RampCollider");
            seg.transform.SetParent(g, false);
            seg.transform.SetPositionAndRotation(basePoint + dir * rMid + Vector3.up * y,
                                                 Quaternion.Euler(0f, a + 90f, 0f) * Quaternion.Euler(-slope, 0f, 0f));
            seg.AddComponent<BoxCollider>().size = new Vector3(rOut - rIn, 0.2f, pieceLen);   // x radial, z along the turn
            // Handrail along the tops of the iron posts.
            var rail = Box(g, "Handrail", basePoint + dir * (rOut - 0.1f) + Vector3.up * (y + 1.05f), new Vector3(0.07f, 0.07f, pieceLen), ironMat);
            rail.transform.rotation = seg.transform.rotation;
            Object.DestroyImmediate(rail.GetComponent<Collider>());
            // Invisible railing on the outer edge, so you can't slide off the side (from the 3rd step up, so you
            // can still step on from the floor).
            if (k < sub * 3) continue;
            var guard = new GameObject("RailGuard");
            guard.transform.SetParent(g, false);
            guard.transform.SetPositionAndRotation(basePoint + dir * (rOut + 0.05f) + Vector3.up * (y + 0.7f), seg.transform.rotation);
            guard.AddComponent<BoxCollider>().size = new Vector3(0.1f, 1.4f, pieceLen);
        }
        // Landing from the top step onto the upper floor.
        float topA = (steps - 0.5f) * turn;
        Vector3 topDir = Quaternion.Euler(0f, topA + turn, 0f) * Vector3.forward;
        Box(g, "Landing", basePoint + topDir * 4.2f + Vector3.up * (FloorTop - 0.15f), new Vector3(2.6f, 0.3f, 3.6f), shelfWoodMat, false, topA + turn);

        foreach (var t in g.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;   // it moves
        g.position += Vector3.up * FloorTop;   // parked in the floor above until area 1 is cleared
        var lower = g.gameObject.AddComponent<SlideShut>();
        lower.slide = Vector3.down * FloorTop;
        lower.duration = 5f;
        return g.gameObject;
    }
}
