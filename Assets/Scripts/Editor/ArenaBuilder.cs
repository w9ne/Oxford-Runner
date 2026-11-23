using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Rebuild Test Arena
/// Replaces everything inside "GrayboxArena" (except the floor and the four outer walls) with four
/// movement-test zones, one per quadrant. The cross-shaped lanes from the four enemy doors to the
/// centre are left clear so the wave spawner still works unchanged.
///
///   NW  Wall-Run Gauntlet - staggered zig-zag panels + a long parallel corridor for chained wall jumps
///   NE  Climb Tower       - a circular "chimney" you scale by wall-jumping across it, inside a
///                           circular run ring for continuous curved wall-running
///   SW  Slide Zone        - low tunnel, a row of low gates, and a slide-jump gap between two ledges
///   SE  Box Yard          - procedurally scattered crates (seeded, non-overlapping) + wall-run slabs
///
/// Safe to re-run; the layout is deterministic.
/// </summary>
public static class ArenaBuilder
{
    private const string MatDir = "Assets/Materials/";
    private const string WallRunTag = "WallRun";
    private const float LaneHalfWidth = 6f;        // keep |x| or |z| below this clear for enemies
    private const float ArenaHalf = 48f;           // usable half-size inside the outer walls

    private static readonly HashSet<string> Keep = new HashSet<string> { "Floor", "Wall_N", "Wall_S", "Wall_E", "Wall_W" };

    private static Material wallRunMat, platformMat, wallMat, goalMat;

    [MenuItem("Oxford Runner/Rebuild Test Arena")]
    public static void Build()
    {
        var arena = GameObject.Find("GrayboxArena");
        if (arena == null)
        {
            EditorUtility.DisplayDialog("Rebuild Test Arena", "No 'GrayboxArena' object in the open scene.", "OK");
            return;
        }

        wallRunMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Graybox_WallRun.mat");
        platformMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Graybox_Platform.mat");
        wallMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Graybox_Wall.mat");
        goalMat = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "Graybox_Goal.mat");

        // Clear the old course (iterate backwards since we're destroying children).
        for (int i = arena.transform.childCount - 1; i >= 0; i--)
        {
            var child = arena.transform.GetChild(i);
            if (!Keep.Contains(child.name)) Undo.DestroyObjectImmediate(child.gameObject);
        }

        var zones = new GameObject("TestZones").transform;
        Undo.RegisterCreatedObjectUndo(zones.gameObject, "Rebuild Test Arena");
        zones.SetParent(arena.transform, false);

        BuildWallRunGauntlet(Group(zones, "NW_WallRunGauntlet"));
        BuildClimbTower(Group(zones, "NE_ClimbTower"), new Vector3(27f, 0f, 27f));
        BuildSlideZone(Group(zones, "SW_SlideZone"));
        BuildBoxYard(Group(zones, "SE_BoxYard"), seed: 1337);

        EditorSceneManager.MarkSceneDirty(arena.scene);
        Debug.Log("[ArenaBuilder] Test arena rebuilt: gauntlet (NW), climb tower (NE), slide zone (SW), box yard (SE).");
    }

    // ------------------------------------------------------------------ NW: wall-run gauntlet
    private static void BuildWallRunGauntlet(Transform root)
    {
        // Zig-zag: alternating left/right panels, 4.5 m apart. Wall-jump from one to the next.
        var zig = Group(root, "ZigZag");
        Box(zig, "Start", new Vector3(-8f, 1f, 23f), new Vector3(4f, 2f, 4f), platformMat);
        for (int i = 0; i < 5; i++)
        {
            float x = -15f - 6f * i;
            float z = i % 2 == 0 ? 20.5f : 25.5f;
            Box(zig, $"Panel_{i + 1}", new Vector3(x, 4.5f, z), new Vector3(10f, 6f, 0.5f), wallRunMat, wallRun: true);
        }
        Box(zig, "Landing", new Vector3(-46.5f, 1.5f, 23f), new Vector3(5f, 3f, 4f), goalMat);

        // Corridor: two long parallel walls. Chain wall jumps back and forth the whole way.
        var corridor = Group(root, "Corridor");
        Box(corridor, "Launch", new Vector3(-9f, 0.6f, 40.25f), new Vector3(3f, 1.2f, 3f), platformMat);
        Box(corridor, "Wall_South", new Vector3(-27f, 3.5f, 38f), new Vector3(30f, 7f, 0.5f), wallRunMat, wallRun: true);
        Box(corridor, "Wall_North", new Vector3(-27f, 3.5f, 42.5f), new Vector3(30f, 7f, 0.5f), wallRunMat, wallRun: true);
        Box(corridor, "Landing", new Vector3(-45f, 1f, 40.25f), new Vector3(5f, 2f, 5f), goalMat);
    }

    // ------------------------------------------------------------------ NE: circular climb tower
    private static void BuildClimbTower(Transform root, Vector3 center)
    {
        // Direction from the tower to the arena centre: the entrances face this way.
        float entranceAngle = Mathf.Atan2(-center.x, -center.z) * Mathf.Rad2Deg;

        // Outer run ring: low circular wall to wall-run around continuously. Gaps on the diagonals.
        BuildRing(Group(root, "RunRing"), center, innerRadius: 10f, thickness: 0.5f, segments: 32,
                  bottom: 0f, height: 6f, mat: wallRunMat, wallRun: true,
                  isGap: a => AngleNear(a, entranceAngle, 12f) || AngleNear(a, entranceAngle + 90f, 12f)
                           || AngleNear(a, entranceAngle + 180f, 12f) || AngleNear(a, entranceAngle - 90f, 12f));

        // Chimney: narrow tall ring. Wall-jump side to side to climb (each jump nets ~1 m of height,
        // and wall jumps refresh the double jump). Door at the bottom facing the arena centre.
        const float r = 3f, t = 0.5f, h = 14f;
        BuildRing(Group(root, "Chimney"), center, r, t, segments: 20, bottom: 0f, height: h,
                  mat: wallRunMat, wallRun: true, isGap: null,
                  doorway: a => AngleNear(a, entranceAngle, 20f), doorHeight: 3f);

        // Rest ledges on the inside wall.
        var ledges = Group(root, "Ledges");
        PlaceOnCircle(ledges, "Ledge_5m", center, r - 0.6f, entranceAngle + 90f, 5f, new Vector3(2f, 0.4f, 1.2f), platformMat);
        PlaceOnCircle(ledges, "Ledge_10m", center, r - 0.6f, entranceAngle - 90f, 10f, new Vector3(2f, 0.4f, 1.2f), platformMat);

        // Summit deck around the outside of the rim; climb out over the top onto it.
        BuildRing(Group(root, "SummitDeck"), center, r + t, 2f, segments: 20, bottom: h - 0.5f, height: 0.5f,
                  mat: goalMat, wallRun: false, isGap: null);
    }

    /// <summary>Builds a ring of box segments. Each segment's thin axis points away from the centre.</summary>
    private static void BuildRing(Transform root, Vector3 center, float innerRadius, float thickness, int segments,
                                  float bottom, float height, Material mat, bool wallRun,
                                  System.Func<float, bool> isGap,
                                  System.Func<float, bool> doorway = null, float doorHeight = 0f)
    {
        float step = 360f / segments;
        float midRadius = innerRadius + thickness * 0.5f;
        // Chord width at the outer face, slightly overlapped so there are no cracks between segments.
        float width = 2f * (innerRadius + thickness) * Mathf.Tan(Mathf.PI / segments) * 1.02f;

        for (int i = 0; i < segments; i++)
        {
            float angle = (i + 0.5f) * step;
            if (isGap != null && isGap(angle)) continue;

            float segBottom = bottom, segHeight = height;
            if (doorway != null && doorway(angle))
            {
                segBottom = bottom + doorHeight;      // lintel above the door opening
                segHeight = height - doorHeight;
            }

            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            Vector3 pos = center + dir * midRadius + Vector3.up * (segBottom + segHeight * 0.5f);
            Box(root, $"Seg_{i:00}", pos, new Vector3(width, segHeight, thickness), mat, wallRun, angle);
        }
    }

    private static void PlaceOnCircle(Transform root, string name, Vector3 center, float radius, float angle,
                                      float topHeight, Vector3 size, Material mat)
    {
        Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        Box(root, name, center + dir * radius + Vector3.up * (topHeight - size.y * 0.5f), size, mat, false, angle);
    }

    private static bool AngleNear(float a, float b, float tolerance) => Mathf.Abs(Mathf.DeltaAngle(a, b)) <= tolerance;

    // ------------------------------------------------------------------ SW: slide zone
    private static void BuildSlideZone(Transform root)
    {
        // Low tunnel: 1.3 m clearance, only passable while sliding (standing capsule is 2 m).
        var tunnel = Group(root, "Tunnel");
        Box(tunnel, "Roof", new Vector3(-22f, 1.55f, -14f), new Vector3(24f, 0.5f, 4f), wallMat);
        Box(tunnel, "Side_N", new Vector3(-22f, 0.65f, -12.25f), new Vector3(24f, 1.3f, 0.5f), wallMat);
        Box(tunnel, "Side_S", new Vector3(-22f, 0.65f, -15.75f), new Vector3(24f, 1.3f, 0.5f), wallMat);

        // Gates: a row of low beams to slide under back-to-back.
        var gates = Group(root, "Gates");
        for (int i = 0; i < 5; i++)
            Gate(gates, $"Gate_{i + 1}", new Vector3(-12f - 6f * i, 0f, -26f), width: 4f);

        // Slide-jump gap: 15 m between ledges. A beam on the take-off forces a slide right before the jump.
        var gap = Group(root, "SlideJumpGap");
        Box(gap, "Step_Up", new Vector3(-8.5f, 0.5f, -38f), new Vector3(3f, 1f, 4f), platformMat);
        Box(gap, "Ledge_TakeOff", new Vector3(-13f, 1f, -38f), new Vector3(6f, 2f, 6f), platformMat);
        Gate(gap, "TakeOff_Beam", new Vector3(-15f, 2f, -38f), width: 6f);
        Box(gap, "Ledge_Landing", new Vector3(-34f, 1f, -38f), new Vector3(6f, 2f, 6f), goalMat);
        Box(gap, "Step_Down", new Vector3(-38.5f, 0.5f, -38f), new Vector3(3f, 1f, 4f), platformMat);
    }

    /// <summary>Two posts and a beam (1.3 m clearance) spanning the Z axis at the given base point.</summary>
    private static void Gate(Transform root, string name, Vector3 basePoint, float width)
    {
        var g = Group(root, name);
        Box(g, "Beam", basePoint + new Vector3(0f, 1.55f, 0f), new Vector3(0.4f, 0.5f, width), wallMat);
        Box(g, "Post_A", basePoint + new Vector3(0f, 0.9f, width * 0.5f + 0.2f), new Vector3(0.4f, 1.8f, 0.4f), wallMat);
        Box(g, "Post_B", basePoint + new Vector3(0f, 0.9f, -width * 0.5f - 0.2f), new Vector3(0.4f, 1.8f, 0.4f), wallMat);
    }

    // ------------------------------------------------------------------ SE: procedural box yard
    private static readonly Vector3[] CrateSizes =
    {
        new Vector3(1f, 1f, 1f), new Vector3(1.5f, 1.5f, 1.5f), new Vector3(2f, 2f, 2f),
        new Vector3(3f, 1.5f, 3f), new Vector3(2f, 3f, 2f), new Vector3(4f, 2f, 2f),
    };

    private struct Placed { public Vector2 pos; public float radius; }

    private static void BuildBoxYard(Transform root, int seed)
    {
        var rng = new System.Random(seed);
        var placed = new List<Placed>();
        var min = new Vector2(LaneHalfWidth + 2f, -ArenaHalf + 2f);
        var max = new Vector2(ArenaHalf - 2f, -LaneHalfWidth - 2f);

        // Hand-placed wall-run slabs first, so crates flow around them.
        var slabs = Group(root, "WallRunSlabs");
        var slabSpots = new[] { (new Vector2(16f, -20f), 0f), (new Vector2(30f, -36f), 90f), (new Vector2(40f, -16f), 45f) };
        foreach (var (p, yaw) in slabSpots)
        {
            Box(slabs, $"Slab_{placed.Count + 1}", new Vector3(p.x, 3f, p.y), new Vector3(0.6f, 6f, 7f), wallRunMat, true, yaw);
            placed.Add(new Placed { pos = p, radius = 3.6f });
        }

        // Rejection sampling: random crate, random spot; keep it only if it clears every placed
        // footprint by a walkable gap. Footprints are approximated by circumscribed circles.
        var crates = Group(root, "Crates");
        const int target = 28, maxAttempts = 600;
        const float walkGap = 1.6f;
        int count = 0;
        for (int attempt = 0; attempt < maxAttempts && count < target; attempt++)
        {
            Vector3 size = CrateSizes[rng.Next(CrateSizes.Length)];
            float radius = new Vector2(size.x, size.z).magnitude * 0.5f;
            var pos = new Vector2(Lerp(rng, min.x + radius, max.x - radius), Lerp(rng, min.y + radius, max.y - radius));

            bool clear = true;
            foreach (var other in placed)
                if ((other.pos - pos).sqrMagnitude < Sq(other.radius + radius + walkGap)) { clear = false; break; }
            if (!clear) continue;

            float yaw = rng.Next(0, 6) * 15f;
            placed.Add(new Placed { pos = pos, radius = radius });
            count++;
            Box(crates, $"Crate_{count:00}", new Vector3(pos.x, size.y * 0.5f, pos.y), size, platformMat, false, yaw);

            // Sometimes stack a smaller crate on top for a climbable step.
            if (rng.NextDouble() < 0.35)
            {
                var top = size * 0.5f;
                top.y = Mathf.Max(top.y, 0.75f);
                var offset = new Vector2(Lerp(rng, -0.25f, 0.25f) * size.x, Lerp(rng, -0.25f, 0.25f) * size.z);
                Box(crates, $"Crate_{count:00}_Top", new Vector3(pos.x + offset.x, size.y + top.y * 0.5f, pos.y + offset.y),
                    top, platformMat, false, yaw + 15f);
            }
        }
        if (count < target) Debug.LogWarning($"[ArenaBuilder] Box yard placed {count}/{target} crates (ran out of space).");
    }

    private static float Lerp(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);
    private static float Sq(float v) => v * v;

    // ------------------------------------------------------------------ helpers
    private static Transform Group(Transform parent, string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        return t;
    }

    private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
                                  bool wallRun = false, float yaw = 0f)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = size;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        if (wallRun) go.tag = WallRunTag;
        go.isStatic = true;
        return go;
    }
}
