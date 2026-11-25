using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 2 dressing (BuildLevel2 calls it; it can also be run on the open hand-edited scene, since it only
/// adds a "Dressing" group and reads where the walls and towers already are):
/// wall torches on the curtain walls and gate walls, wooden doors at the foot of the towers and in the keep,
/// grass and flowers across the grounds (merged meshes) and a thicker forest outside the walls.
/// </summary>
public static partial class LevelBuilder
{
    private const float CastleHalfW = 55f;

    public static void Level2Dressing(Transform level)
    {
        foreach (var path in new[] { "Dressing", "Grounds/Scenery_Trees" })   // Scenery_Trees: the old tree pass, now part of Forest
        {
            var old = level.Find(path);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var g = Graybox.Group(level, "Dressing");
        CastleTorches(Graybox.Group(g, "Torches"));
        TowerDoors(Graybox.Group(g, "Doors"), level);
        Physics.SyncTransforms();
        GroundPlants(Graybox.Group(g, "Plants"));
        Forest(Graybox.Group(g, "Forest"));
    }

    // ------------------------------------------------------------------ torches
    private static void CastleTorches(Transform g)
    {
        const float h = 4.2f;
        // Inner faces of the side curtain walls.
        for (float z = -4f; z < 192f; z += 24f)
            for (int s = -1; s <= 1; s += 2)
                WallTorch(g, new Vector3(s * (CastleHalfW - 0.1f), h, z), s > 0 ? -90f : 90f);
        // Both faces of the two gate walls, flanking the gate towers.
        foreach (float z in new[] { 62f, 136f })
            foreach (float x in new[] { -30f, -14f, 14f, 30f })
            {
                WallTorch(g, new Vector3(x, h, z - 1.6f), 180f);
                WallTorch(g, new Vector3(x, h, z + 1.6f), 0f);
            }
        foreach (float x in new[] { -30f, -10f, 10f, 30f })
        {
            WallTorch(g, new Vector3(x, h, -14.6f), 0f);       // start wall
            WallTorch(g, new Vector3(x, h, 194.4f), 180f);     // castle wall
        }
        foreach (var l in g.GetComponentsInChildren<Light>()) { l.range = 8f; l.intensity = 1.3f; }   // daylight: a warm glow, not a lamp
    }

    // ------------------------------------------------------------------ doors
    // A wooden door at the foot of every round tower (facing the start), and a great door in the keep wall.
    private static void TowerDoors(Transform g, Transform level)
    {
        foreach (var r in level.GetComponentsInChildren<MeshRenderer>())
        {
            if (r.name != "RoundTower") continue;
            Vector3 c = r.transform.position, s = r.transform.lossyScale;
            float radius = s.x * 0.5f, baseY = c.y - s.y;
            if (radius < 1.9f) continue;
            WallDoor(g, new Vector3(c.x, baseY, c.z) + Vector3.back * (radius - 0.15f), 180f, 1.8f, 3f);
        }
        WallDoor(g, new Vector3(0f, 0f, 194.4f), 180f, 6f, 8f);   // the keep
        foreach (int s in new[] { -1, 1 }) WallDoor(g, new Vector3(s * 28f, 0f, -14.4f), 0f, 2f, 3.2f);   // postern doors by the start
    }

    /// <summary>A plank door with iron bands, a ring handle and a stone frame. basePoint = bottom middle, front faces yaw. Decorative (no colliders).</summary>
    private static void WallDoor(Transform parent, Vector3 basePoint, float yaw, float width, float height)
    {
        var d = Graybox.Group(parent, "Door");
        var iron = Mat("L2_GateIron", new Color(0.18f, 0.18f, 0.2f));
        Quaternion r = Quaternion.Euler(0f, yaw, 0f);
        Vector3 At(float x, float y, float z) => basePoint + r * new Vector3(x, y, z);
        Box(d, "Leaf", At(0f, height * 0.5f, 0f), new Vector3(width, height, 0.5f), woodMat, false, yaw);
        for (int i = 0; i < Mathf.Max(2, Mathf.RoundToInt(width / 1.5f)); i++)   // plank seams
        {
            float x = -width * 0.5f + width * (i + 0.5f) / Mathf.Max(2, Mathf.RoundToInt(width / 1.5f));
            Box(d, "Seam", At(x, height * 0.5f, 0.26f), new Vector3(0.05f, height - 0.1f, 0.03f), iron, false, yaw);
        }
        foreach (float y in new[] { 0.22f, 0.78f })
            Box(d, "Band", At(0f, height * y, 0.28f), new Vector3(width - 0.1f, 0.18f, 0.06f), iron, false, yaw);
        Box(d, "Ring", At(width * 0.3f, height * 0.45f, 0.33f), new Vector3(0.22f, 0.22f, 0.06f), iron, false, yaw);
        for (int s = -1; s <= 1; s += 2)
            Box(d, "Jamb", At(s * (width * 0.5f + 0.2f), height * 0.5f + 0.1f, 0.05f), new Vector3(0.4f, height + 0.2f, 0.7f), stoneMat, false, yaw);
        Box(d, "Lintel", At(0f, height + 0.35f, 0.05f), new Vector3(width + 0.8f, 0.5f, 0.7f), stoneMat, false, yaw);
        Box(d, "Step", At(0f, 0.1f, 0.5f), new Vector3(width + 0.4f, 0.2f, 0.6f), stoneMat, false, yaw);
        foreach (var c in d.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);   // never snag a wall-run
    }

    // ------------------------------------------------------------------ grass + flowers
    // Clumps of grass and patches of flowers on open ground (raycast: only where the ground is the top surface),
    // thicker along the foot of the walls. Merged into a few meshes.
    private static void GroundPlants(Transform g)
    {
        var rng = new System.Random(20261125);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        string[] grass = { Nature + "Vegetation/Grass/rpgpp_lt_grass_small_01a.prefab", SNP + "Grass_02.prefab" };   // the light ones (~200 verts)
        string[] flowers = { Nature + "Vegetation/Flowers/rpgpp_lt_flower_01.prefab", Nature + "Vegetation/Flowers/rpgpp_lt_flower_02.prefab", Nature + "Vegetation/Flowers/rpgpp_lt_flower_03.prefab",
                             SNP + "Flowers_01.prefab", SNP + "Flowers_02.prefab", Nature + "Vegetation/Plants/rpgpp_lt_plant_01.prefab" };
        string[] bushes = { SNP + "Bush_01.prefab", SNP + "Bush_02.prefab", Nature + "Vegetation/Bushes/rpgpp_lt_bush_01.prefab", Nature + "Vegetation/Bushes/rpgpp_lt_bush_02.prefab" };

        bool Open(float x, float z)
        {
            if (!Physics.Raycast(new Vector3(x, 40f, z), Vector3.down, out var hit, 60f, ~0, QueryTriggerInteraction.Ignore)) return false;
            return hit.collider.name == "Ground" && hit.point.y < 0.1f;
        }
        void Plant(string[] kinds, float x, float z, float s0, float s1)
        {
            if (Open(x, z)) Prop(g, kinds[rng.Next(kinds.Length)], new Vector3(x, 0f, z), R(0f, 360f), R(s0, s1));
        }

        for (int p = 0; p < 200; p++)   // grass clumps
        {
            float cx = R(-52f, 52f), cz = R(-12f, 192f);
            for (int i = 0; i < 6; i++) Plant(grass, cx + R(-2.5f, 2.5f), cz + R(-2.5f, 2.5f), 1f, 1.8f);
        }
        for (int p = 0; p < 60; p++)    // flower patches (flowers are ~1000 verts each)
        {
            float cx = R(-52f, 52f), cz = R(-12f, 192f);
            for (int i = 0; i < 5; i++) Plant(flowers, cx + R(-1.8f, 1.8f), cz + R(-1.8f, 1.8f), 0.9f, 1.4f);
        }
        for (int i = 0; i < 400; i++)   // loose grass everywhere
            Plant(grass, R(-53f, 53f), R(-13f, 193f), 0.9f, 1.5f);
        for (float z = -12f; z < 192f; z += R(2f, 4f))   // along the foot of the side walls
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (CastleHalfW - R(0.6f, 2f));
                if (rng.NextDouble() < 0.25) Plant(bushes, x, z, 0.8f, 1.2f);
                else Plant(rng.NextDouble() < 0.5 ? flowers : grass, x, z, 1f, 1.5f);
            }
        EnableInstancing(g);   // not merged: merged meshes are stored in the scene as text (made Level2.unity 64 MB)
    }

    // ------------------------------------------------------------------ forest
    // A grass apron and big trees outside the curtain walls, tall enough to show over them. Background only.
    private static void Forest(Transform g)
    {
        Box(g, "OuterGround", new Vector3(0f, -0.6f, 90f), new Vector3(500f, 1f, 600f), groundMat);
        var rng = new System.Random(20261126);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        string[] kinds = { SNP + "Tree_01.prefab", SNP + "Tree_02.prefab", SNP + "Tree_03.prefab", SNP + "Tree_04.prefab", SNP + "Tree_05.prefab",
                           Nature + "Vegetation/Trees/rpgpp_lt_tree_01.prefab", Nature + "Vegetation/Trees/rpgpp_lt_tree_02.prefab", Nature + "Vegetation/Trees/rpgpp_lt_tree_pine_01.prefab" };
        void Tree(float x, float z, float big)
        {
            string k = kinds[rng.Next(kinds.Length)];
            Prop(g, k, new Vector3(x, 0f, z), R(0f, 360f), (k.Contains("rpgpp") ? R(2.4f, 3.4f) : R(4f, 5.5f)) * big);
        }
        for (int i = 0; i < 70; i++) Tree((i % 2 == 0 ? -1f : 1f) * R(62f, 80f), R(-30f, 215f), 1.25f);   // close to the walls: biggest
        for (int i = 0; i < 98; i++) Tree((i % 2 == 0 ? -1f : 1f) * R(80f, 160f), R(-60f, 260f), 1f);
        for (int i = 0; i < 30; i++) Tree(R(-140f, 140f), R(202f, 280f), 1.2f);                         // behind the castle
        for (int i = 0; i < 36; i++) Tree(R(-110f, 110f), R(-70f, -22f), 1.1f);                         // behind the start
    }

    // GPU instancing on every material under g: copies of the same plant draw in a few batches.
    private static void EnableInstancing(Transform g)
    {
        foreach (var r in g.GetComponentsInChildren<Renderer>())
            foreach (var m in r.sharedMaterials)
                if (m != null && !m.enableInstancing) { m.enableInstancing = true; UnityEditor.EditorUtility.SetDirty(m); }
    }
}
