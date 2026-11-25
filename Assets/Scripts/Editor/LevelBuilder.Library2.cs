using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Level 3, area 2 "Upper Hall" (y 13 up): the floor above the ground-floor library, wrecked by the fighting.
///   You come up the spiral staircase at the rotunda; a hatch slides shut over it behind you.
///   Two storeys: the hall floor (y 13) and a U-shaped balcony (y 19) along both side walls and the back wall.
///   A grand staircase in the middle (central flight to a landing, then a flight to each side balcony).
///   The front of the hall (z &lt; 50) has caved in: a rubble pile up to the roof closes it off.
///   A giant metal door in the back wall at balcony level leads to area 3 (boss room; for now a corridor with
///   the exit portal). Damage: toppled / leaning / broken bookcases, a fallen chandelier, overturned tables,
///   broken balcony railings, rubble, soot marks, books everywhere.
/// </summary>
public static partial class LevelBuilder
{
    private const float BalconyY = 19f, DoorTop = 27f, HallZ0 = 50f, BalconyIn = 20f, BackBalconyZ = 106f;
    private static Material doorMetalMat, sootMat;

    /// <summary>Builds area 2 and area 3 (the door corridor). Returns the exit portal.</summary>
    private static GameObject UpperHall(Transform a1, Transform a2, GameObject stairs)
    {
        doorMetalMat = Mat("L3_DoorMetal", new Color(0.34f, 0.34f, 0.38f));
        sootMat = Mat("L3_Soot", new Color(0.06f, 0.05f, 0.05f));
        var rng = new System.Random(3304);

        var hatch = Hatch(a2, stairs);
        Collapse(a2, rng);
        Balconies(a2, rng);
        GrandStaircase(a2);
        HallWalls(a2, rng);
        HallStacks(a2, rng);
        HallLights(a2);
        HallDebris(a2, rng);
        var torches = new[] { GiantTorch(a2, -1), GiantTorch(a2, 1) };
        var a3 = Group("C_SealedDoor");
        var door = MetalDoor(a3);
        var portal = DoorCorridor(a3);

        var garrison = new List<Enemy>
        {
            FloatingMage(a2, new Vector3(-10f, 23f, 82f)),
            FloatingMage(a2, new Vector3(10f, 23f, 104f)),
            FloatingMage(a2, new Vector3(-8f, 22.5f, 64f)),
            FloatingMage(a2, new Vector3(9f, 22.5f, 74f)),
            FloatingMage(a2, new Vector3(0f, 24f, 100f)),
            FloatingMage(a2, new Vector3(-13f, 23.5f, 96f)),
            FloatingMage(a2, new Vector3(-26f, BalconyY, 116f), hover: false),
            FloatingMage(a2, new Vector3(26f, BalconyY, 116f), hover: false),
        };
        var hall = Encounter("Upper Hall", a2, new Vector3(0f, FloorTop, 95f), new Vector2(LibHalfW * 2f, 50f), garrison,
                             // Hall floor in front of the grand staircase, and its landing. Nothing behind it: walkers spawned
                             // back there walked straight at you into the staircase and got stuck.
                             new[] { new Vector3(-24f, FloorTop, 58f), new Vector3(24f, FloorTop, 58f), new Vector3(-24f, FloorTop, 84f), new Vector3(24f, FloorTop, 84f),
                                     new Vector3(0f, 15f, 92f) },
                             Wave(10, 70f, 4.4f, 2.2f), Wave(12, 75f, 4.6f, 2f));   // 22 at once: 2 commanders (auto), ~10 mages, ~10 skeletons
        hall.mageSpawnPoints = SpawnPoints(a2, "MageSpawnPoints", new Vector3(0f, FloorTop, 95f),   // balconies: mages only
                                           new[] { new Vector3(-26f, BalconyY, 100f), new Vector3(26f, BalconyY, 98f), new Vector3(0f, BalconyY, 112f) });
        hall.garrisonWakesOnEnter = true;   // the floating mages don't shoot down the stair hole while you climb
        // Starts once you're off the stairs (z > 70), so the hatch never shuts under you.
        hall.triggerArea = new Bounds(new Vector3(0f, FloorTop + 7f, 95f), new Vector3(LibHalfW * 2f, 14f, 50f));
        hall.gates.Add(stairs);
        hall.closeOnEnter.AddRange(hatch);
        var cp = new GameObject("Checkpoint_UpperHall").transform;
        cp.SetParent(a2, false);
        cp.position = new Vector3(0f, FloorTop + 1.05f, 72f);
        hall.checkpoint = cp;
        hall.unloadOnEnter.Add(a1.gameObject);   // the ground floor is shut away under the hatch

        // Area 3: clearing the hall opens the door and lights the giant torches. Walking through shuts the door
        // behind you and unloads the hall. (z > 125: clear of the door leaves' swing.)
        var sealed_ = Encounter("Sealed Door", a3, new Vector3(0f, BalconyY, 132.5f), new Vector2(8f, 15f), new List<Enemy>(), new Vector3[0]);
        sealed_.triggerArea = new Bounds(new Vector3(0f, BalconyY + 4f, 132.5f), new Vector3(8f, 8f, 15f));
        sealed_.gates.Add(door);
        sealed_.gates.AddRange(torches);
        sealed_.closeOnEnter.Add(door);
        sealed_.unloadOnEnter.Add(a2.gameObject);
        var cp3 = new GameObject("Checkpoint_SealedDoor").transform;
        cp3.SetParent(a3, false);
        cp3.position = new Vector3(0f, BalconyY + 1.05f, 127f);
        sealed_.checkpoint = cp3;
        return portal;
    }

    // Two plank-and-iron halves hidden in the floor either side of the stair hole; they slide shut over it.
    private static GameObject[] Hatch(Transform parent, GameObject stairs)
    {
        var halves = new GameObject[2];
        for (int s = -1; s <= 1; s += 2)
        {
            var h = Graybox.Group(parent, s < 0 ? "Hatch_L" : "Hatch_R");
            float cx = s * (HoleHalf * 1.5f);   // open: tucked inside the floor slab, beside the hole
            Box(h, "Planks", new Vector3(cx, FloorTop - 0.25f, RotundaZ), new Vector3(HoleHalf, 0.4f, HoleHalf * 2f), shelfWoodMat);
            for (int b = -1; b <= 1; b++)
                Box(h, "Band", new Vector3(cx, FloorTop - 0.06f, RotundaZ + b * 3.5f), new Vector3(HoleHalf - 0.2f, 0.04f, 0.3f), ironMat);
            foreach (var t in h.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;   // it moves
            var shut = h.gameObject.AddComponent<SlideShut>();
            shut.slide = new Vector3(-s * HoleHalf, 0f, 0f);
            if (s > 0) shut.hideAfter = stairs;
            halves[(s + 1) / 2] = h.gameObject;
        }
        return halves;
    }

    // ------------------------------------------------------------------ collapse
    // The front of the hall fell in: a heap of stone, roof beams and bookcases from wall to wall, up to the roof.
    private static void Collapse(Transform parent, System.Random rng)
    {
        var g = Graybox.Group(parent, "Collapse");
        var wall = new GameObject("CollapseBlocker");   // what actually stops you; the heap is visual
        wall.transform.SetParent(g, false);
        wall.transform.position = new Vector3(0f, (FloorTop + Roof) * 0.5f, HallZ0 - 1.5f);
        wall.AddComponent<BoxCollider>().size = new Vector3(LibHalfW * 2f, Roof - FloorTop, 3f);

        var heap = Graybox.Group(g, "Heap");
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        for (int i = 0; i < 140; i++)
        {
            float x = R(-LibHalfW, LibHalfW), depth = R(0f, 6f);   // depth: how far it spilled into the hall
            float maxH = Mathf.Lerp(Roof - FloorTop, 1f, depth / 6f);
            float size = R(1.2f, 4.5f) * Mathf.Lerp(1f, 0.4f, depth / 6f);
            var c = Box(heap, "Chunk", new Vector3(x, FloorTop + R(0f, maxH), HallZ0 - 2f + depth), Vector3.one * size, rng.NextDouble() < 0.75 ? stoneMat : roofMat);
            c.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
        }
        // Roof beams that came down with it, jutting out of the heap.
        for (int i = 0; i < 6; i++)
        {
            var b = Box(heap, "FallenBeam", new Vector3(R(-26f, 26f), FloorTop + R(3f, 9f), HallZ0 + 1f), new Vector3(0.8f, 0.7f, R(8f, 12f)), roofMat);
            b.transform.rotation = Quaternion.Euler(R(-55f, -30f), R(-25f, 25f), R(-10f, 10f));
        }
        MergeByMaterial(heap, 200f);
        // Bookcases crushed against the heap.
        foreach (float x in new[] { -24f, -9f, 13f, 27f })
        {
            var b = Bookcase(g, new Vector3(x, FloorTop, HallZ0 + 1.5f), 6f + R(0f, 3f), 6f, R(-15f, 15f));
            b.RotateAround(new Vector3(x, FloorTop, HallZ0 + 1f), Vector3.right, -R(20f, 35f));   // leaning back into it
        }
    }

    // ------------------------------------------------------------------ balconies
    private static void Balconies(Transform parent, System.Random rng)
    {
        var g = Graybox.Group(parent, "Balcony");
        const float thick = 0.6f;
        for (int s = -1; s <= 1; s += 2)
        {
            float x0 = s < 0 ? -LibHalfW : BalconyIn, x1 = s < 0 ? -BalconyIn : LibHalfW;
            Slab(g, "BalconyFloor", x0, x1, HallZ0, LibLength, BalconyY, thick, groundMat);
            // Pillars under the inner edge, beams under the floor.
            for (float z = 56f; z < LibLength; z += 10f)
            {
                Box(g, "Pillar", new Vector3(s * (BalconyIn + 0.5f), (FloorTop + BalconyY) * 0.5f, z), new Vector3(1f, BalconyY - FloorTop, 1f), stoneMat);
                Box(g, "UnderBeam", new Vector3(s * (BalconyIn + LibHalfW) * 0.5f, BalconyY - thick - 0.3f, z), new Vector3(LibHalfW - BalconyIn, 0.6f, 0.5f), roofMat);
            }
            // Railing along the inner edge; gaps at the stairs, at the leaning-bookcase ramps and where it broke.
            // Openings: stairs, the steps route, then per side the leaning-bookcase ramp, the wall-jump shaft and a broken bit.
            var gaps = new List<Vector2> { new Vector2(88.5f, 95.5f), new Vector2(97.5f, 102.5f) };
            gaps.AddRange(s < 0 ? new[] { new Vector2(58.5f, 63.5f), new Vector2(69.5f, 74.5f), new Vector2(110f, 112f) }
                                : new[] { new Vector2(77f, 82f), new Vector2(63.5f, 68.5f), new Vector2(112f, 114f) });
            Railing(g, new Vector3(s * (BalconyIn + 0.15f), BalconyY, HallZ0), new Vector3(s * (BalconyIn + 0.15f), BalconyY, LibLength), gaps, rng);
        }
        Slab(g, "BalconyFloor", -BalconyIn, BalconyIn, BackBalconyZ, LibLength, BalconyY, thick, groundMat);
        foreach (float x in new[] { -10f, 10f })
            Box(g, "Pillar", new Vector3(x, (FloorTop + BalconyY) * 0.5f, BackBalconyZ + 0.5f), new Vector3(1f, BalconyY - FloorTop, 1f), stoneMat);
        Railing(g, new Vector3(-BalconyIn, BalconyY, BackBalconyZ + 0.15f), new Vector3(BalconyIn, BalconyY, BackBalconyZ + 0.15f),
                new List<Vector2> { new Vector2(4f, 7.5f) }, rng);

        // Bookcases that toppled against the balcony edge: lying on their backs, a walkable ramp up (one per side).
        LeaningCase(g, new Vector3(-8.5f, FloorTop, 61f), new Vector3(-BalconyIn, BalconyY, 61f), 3.4f);
        LeaningCase(g, new Vector3(8.5f, FloorTop, 79.5f), new Vector3(BalconyIn, BalconyY, 79.5f), 3.4f);
    }

    /// <summary>Wooden balustrade from a to b (along one axis). gaps = ranges (along that axis) with no railing.</summary>
    private static void Railing(Transform parent, Vector3 a, Vector3 b, List<Vector2> gaps, System.Random rng)
    {
        bool alongZ = Mathf.Abs(b.z - a.z) > Mathf.Abs(b.x - a.x);
        float t0 = alongZ ? a.z : a.x, t1 = alongZ ? b.z : b.x;
        Vector3 At(float t, float y) => alongZ ? new Vector3(a.x, y, t) : new Vector3(t, y, a.z);
        float start = t0;
        var cuts = new List<Vector2>(gaps) { new Vector2(t1, t1) };
        cuts.Sort((p, q) => p.x.CompareTo(q.x));
        foreach (var gap in cuts)
        {
            float end = Mathf.Min(gap.x, t1);
            if (end - start > 0.6f)
            {
                float len = end - start, mid = (start + end) * 0.5f;
                Vector3 size = alongZ ? new Vector3(0.15f, 0.12f, len) : new Vector3(len, 0.12f, 0.15f);
                Box(parent, "Rail", At(mid, a.y + 1.1f), size, shelfWoodMat);
                var low = Box(parent, "RailLow", At(mid, a.y + 0.35f), size, shelfWoodMat);
                Object.DestroyImmediate(low.GetComponent<Collider>());
                var stop = Box(parent, "RailBlock", At(mid, a.y + 0.6f), alongZ ? new Vector3(0.15f, 1.2f, len) : new Vector3(len, 1.2f, 0.15f), shelfWoodMat);
                stop.GetComponent<MeshRenderer>().enabled = false;   // the actual barrier
                for (float t = start + 0.2f; t <= end - 0.1f; t += 1.2f)
                {
                    var post = Box(parent, "Baluster", At(t, a.y + 0.55f), new Vector3(0.12f, 1.1f, 0.12f), shelfWoodMat);
                    Object.DestroyImmediate(post.GetComponent<Collider>());
                }
            }
            // A splintered stub sticking out where it broke (not at the planned openings).
            if (gap.y > gap.x && rng.NextDouble() < 0.5)
            {
                var stub = Box(parent, "BrokenRail", At(gap.x + 0.5f, a.y + 0.9f), alongZ ? new Vector3(0.15f, 0.12f, 1.2f) : new Vector3(1.2f, 0.12f, 0.15f), shelfWoodMat);
                stub.transform.rotation = Quaternion.Euler(alongZ ? -35f : 0f, 0f, alongZ ? 0f : 35f);
                Object.DestroyImmediate(stub.GetComponent<Collider>());
            }
            start = Mathf.Max(start, gap.y);
        }
    }

    /// <summary>A bookcase lying on its back from bottom to top (books facing up): a walkable slope.</summary>
    private static void LeaningCase(Transform parent, Vector3 bottom, Vector3 top, float width)
    {
        const float thick = 0.8f;
        var g = Graybox.Group(parent, "LeaningBookcase");
        Vector3 d = top - bottom;
        var rot = Quaternion.LookRotation(d);
        Vector3 c = (bottom + top) * 0.5f + rot * Vector3.down * (thick * 0.5f);
        Box(g, "Books", c, new Vector3(width, thick, d.magnitude + 0.4f), shelfMat).transform.rotation = rot;
        for (int s = -1; s <= 1; s += 2)
            Box(g, "Side", c + rot * new Vector3(s * (width * 0.5f + 0.08f), 0.1f, 0f), new Vector3(0.16f, thick + 0.2f, d.magnitude + 0.4f), shelfWoodMat).transform.rotation = rot;
    }

    // ------------------------------------------------------------------ grand staircase
    private static void GrandStaircase(Transform parent)
    {
        var g = Graybox.Group(parent, "GrandStaircase");
        const float landY = 15f;   // 2 m: a double jump gets you onto it
        Flight(g, new Vector3(0f, FloorTop, 76f), new Vector3(0f, landY, 88f), 10f, 12, FloorTop);
        Box(g, "Landing", new Vector3(0f, (FloorTop + landY) * 0.5f, 92f), new Vector3(16f, landY - FloorTop, 8f), stoneMat);
        Box(g, "LandingRug", new Vector3(0f, landY + 0.01f, 92f), new Vector3(12f, 0.02f, 6f), rugMat);
        var stairRug = Box(g, "StairRug", new Vector3(0f, FloorTop + (landY - FloorTop) / 12f * 6.5f, 81.5f), new Vector3(4f, 0.02f, 12.4f), rugMat);   // over the step noses
        stairRug.transform.rotation = Quaternion.Euler(-Mathf.Atan2(landY - FloorTop, 12f) * Mathf.Rad2Deg, 0f, 0f);
        Object.DestroyImmediate(stairRug.GetComponent<Collider>());
        for (int s = -1; s <= 1; s += 2)
        {
            // Solid down to the hall floor: open space under these flights trapped skeletons chasing you upstairs.
            Flight(g, new Vector3(s * 8f, landY, 92f), new Vector3(s * BalconyIn, BalconyY, 92f), 6f, 12, FloorTop);
            // Newel posts with lamps at the foot of the stairs.
            Box(g, "Newel", new Vector3(s * 5.4f, FloorTop + 0.8f, 75.6f), new Vector3(0.6f, 1.6f, 0.6f), shelfWoodMat);
            Prop(g, Olive + "Candles 1.prefab", new Vector3(s * 5.4f, FloorTop + 1.6f, 75.6f), 0f, 1.4f);
        }
    }

    /// <summary>
    /// A straight flight of visual steps from bottom to top (centres of the walking surface at each end).
    /// solidTo = the floor the steps are solid down to. A hidden ramp along the step
    /// noses carries the player (the controller can't climb steps).
    /// </summary>
    private static void Flight(Transform parent, Vector3 bottom, Vector3 top, float width, int steps, float solidTo)
    {
        Vector3 d = top - bottom; d.y = 0f;
        float run = d.magnitude, rise = top.y - bottom.y, stepRun = run / steps, stepRise = rise / steps;
        Vector3 dir = d / run;
        float yaw = Quaternion.LookRotation(dir).eulerAngles.y;
        for (int i = 0; i < steps; i++)
        {
            float topY = bottom.y + stepRise * (i + 1);
            float baseY = solidTo;
            Vector3 c = bottom + dir * (stepRun * (i + 0.5f));
            Box(parent, "Step", new Vector3(c.x, (topY + baseY) * 0.5f, c.z), new Vector3(width, topY - baseY, stepRun + 0.02f), shelfWoodMat, false, yaw);
            Box(parent, "Nosing", new Vector3(c.x, topY - 0.04f, c.z) - dir * (stepRun * 0.5f - 0.05f), new Vector3(width, 0.1f, 0.12f), ironMat, false, yaw)
                .GetComponent<Collider>().enabled = false;
        }
        // Ramp: from one step-run before the first step (on the floor) to the last step's nose.
        Vector3 p0 = bottom - dir * stepRun, p1 = bottom + dir * (run - stepRun) + Vector3.up * rise;
        Vector3 along = p1 - p0;
        var rot = Quaternion.LookRotation(along);
        var ramp = new GameObject("StairRamp");
        ramp.transform.SetParent(parent, false);
        ramp.transform.SetPositionAndRotation((p0 + p1) * 0.5f + rot * Vector3.down * 0.15f, rot);
        ramp.AddComponent<BoxCollider>().size = new Vector3(width, 0.3f, along.magnitude);
        // Banisters along both sides.
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (s * (width * 0.5f - 0.1f));
            var rail = Box(parent, "Banister", (p0 + p1) * 0.5f + side + Vector3.up * 1.1f, new Vector3(0.18f, 0.15f, along.magnitude), shelfWoodMat);
            rail.transform.rotation = rot;
            Object.DestroyImmediate(rail.GetComponent<Collider>());
        }
    }

    // ------------------------------------------------------------------ walls
    // Bookcases on both levels along the side walls (pilasters with torches between them), some knocked to pieces.
    private static void HallWalls(Transform parent, System.Random rng)
    {
        var g = Graybox.Group(parent, "HallWalls");
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const float lowH = 4.4f, highH = 7.5f;   // low: clears the beams under the balcony
        for (int s = -1; s <= 1; s += 2)
        {
            float x = s * (LibHalfW - 0.5f);
            int n = 0;
            for (float z = HallZ0 + 2f; z + 11.6f <= LibLength - 1f; z += 11.6f, n++)
            {
                Pilaster(g, new Vector3(s * (LibHalfW - 0.7f), FloorTop, z), s);
                if (n % 2 == 0) WallTorch(g, new Vector3(s * (LibHalfW - 1.45f), BalconyY + 4f, z), s > 0 ? -90f : 90f);
                bool broken = rng.NextDouble() < 0.35;
                Bookcase(g, new Vector3(x, FloorTop, z + 5.8f), 10f, broken ? R(1.5f, 3f) : lowH, 90f);
                broken = rng.NextDouble() < 0.35;
                Bookcase(g, new Vector3(x, BalconyY, z + 5.8f), 10f, broken ? R(2f, 4f) : highH, 90f);
            }
        }
        // Back wall: low bookcases under the back balcony, and either side of the metal door above it.
        foreach (float x in new[] { -24f, -10f, 10f, 24f, 0f })
            Bookcase(g, new Vector3(x, FloorTop, LibLength - 0.5f), x == 0f ? 6f : 12f, lowH, 0f);
        foreach (float x in new[] { -12.5f, 12.5f, -25.5f, 25.5f })
            Bookcase(g, new Vector3(x, BalconyY, LibLength - 0.5f), x * x < 200f ? 13f : 11f, highH, 0f);
    }

    // ------------------------------------------------------------------ stacks
    private static void HallStacks(Transform parent, System.Random rng)
    {
        var g = Graybox.Group(parent, "HallStacks");
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const float underBalcony = 4.6f;

        // Ways up to the balcony besides the stairs and the two leaning bookcases.
        WallJumpShaft(g, -1, 72f);
        WallJumpShaft(g, 1, 66f);
        BrokenSteps(g, -1, 100f);
        BrokenSteps(g, 1, 100f);

        // Front right: a long bookcase to wall-run; left: one leaning over. One toppled by the stairs for cover.
        Bookcase(g, new Vector3(14f, FloorTop, 57f), 10f, 6f, 90f);
        Lean(Bookcase(g, new Vector3(-14f, FloorTop, 80f), 9f, 6f, 90f), new Vector3(-14f, FloorTop, 80f), Vector3.forward, 14f);
        Topple(g, new Vector3(12.5f, FloorTop, 87f), 7f, 5f, 0f, Vector3.back);

        // Back of the hall, under the back balcony: one standing, one down, two snapped off low.
        Bookcase(g, new Vector3(-13f, FloorTop, 108f), 9f, R(1.6f, 2.6f), 0f);
        Bookcase(g, new Vector3(13f, FloorTop, 108f), 9f, underBalcony, 0f);
        Topple(g, new Vector3(-13f, FloorTop, 115f), 9f, 4.5f, R(-10f, 10f), Vector3.back);
        Bookcase(g, new Vector3(13f, FloorTop, 115f), 9f, R(1.6f, 2.6f), 0f);

        // Overturned reading tables (cover).
        foreach (var t in new[] { new Vector3(-5f, 72f, 90f), new Vector3(6f, 104f, 0f), new Vector3(24f, 58f, 70f) })
        {
            var table = Prop(g, RpgProps + "Tables/rpgpp_lt_table_01.prefab", new Vector3(t.x, FloorTop, t.y), t.z, 1.3f, keepColliders: true);
            if (table != null) { table.transform.rotation = Quaternion.Euler(0f, t.z, 0f) * Quaternion.Euler(0f, 0f, 90f); table.transform.position += Vector3.up * 0.6f; }
        }
        // The chandelier that came down, on its side in the back of the hall.
        var fallen = Graybox.Group(g, "FallenChandelier");
        Chandelier(fallen, new Vector3(-3f, FloorTop + 0.9f, 101f), FloorTop + 1.6f);
        fallen.RotateAround(new Vector3(-3f, FloorTop, 101f), new Vector3(1f, 0f, 0.4f), 28f);
        foreach (var l in fallen.GetComponentsInChildren<Light>()) { l.intensity = 0.9f; l.range = 12f; l.renderMode = LightRenderMode.Auto; l.GetComponent<LightFlicker>().amount = 0.5f; }
    }

    /// <summary>Two tall bookcases 3.5 m apart running out to the balcony edge at z: wall-jump between them up onto the balcony.</summary>
    private static void WallJumpShaft(Transform parent, int side, float z)
    {
        const float x0 = 10f, x1 = BalconyIn - 0.2f, height = BalconyY + 0.7f - FloorTop;   // top just above the balcony
        foreach (float dz in new[] { -2.25f, 2.25f })
            Bookcase(parent, new Vector3(side * (x0 + x1) * 0.5f, FloorTop, z + dz), x1 - x0, height, 0f);
    }

    /// <summary>Two snapped-off bookcases a double jump apart (floor, 2.4 m, 4.8 m, balcony): the easy way up.</summary>
    private static void BrokenSteps(Transform parent, int side, float z)
    {
        Bookcase(parent, new Vector3(side * 14.5f, FloorTop, z), 4f, 2.1f, 90f, 2.4f);
        Bookcase(parent, new Vector3(side * 17.6f, FloorTop, z), 4f, 4.5f, 90f, 2.4f);
    }

    // Tips a bookcase over so it lies on its back (fall = horizontal direction it falls toward, across its length).
    private static void Topple(Transform parent, Vector3 basePoint, float length, float height, float yaw, Vector3 fall)
    {
        var b = Bookcase(parent, basePoint, length, height, yaw);
        Vector3 axis = Vector3.Cross(Vector3.up, fall);
        b.RotateAround(basePoint + Vector3.up * 0.5f, axis, 90f);
    }

    private static void Lean(Transform b, Vector3 pivot, Vector3 axis, float angle) => b.RotateAround(pivot, axis, angle);

    // ------------------------------------------------------------------ lights
    private static void HallLights(Transform parent)
    {
        var g = Graybox.Group(parent, "HallLights");
        Chandelier(g, new Vector3(0f, 23f, 86f), Roof);
        Chandelier(g, new Vector3(0f, 23f, 110f), Roof);
        // Damaged: the torches gutter.
        foreach (var f in parent.GetComponentsInChildren<LightFlicker>()) if (f.name == "TorchLight") f.amount = 0.45f;
        // Dust hanging in the air.
        foreach (var p in new[] { new Vector3(0f, 18f, 80f), new Vector3(-18f, 16f, 108f), new Vector3(18f, 16f, 66f), new Vector3(0f, 18f, 56f) })
            Prop(g, Dungeon + "Particles_Dust.prefab", p, 0f, 3f);
    }

    // ------------------------------------------------------------------ debris
    // Rubble, soot marks, torn-up rug and books all over both floors. Visual only, merged into a few meshes.
    private static void HallDebris(Transform parent, System.Random rng)
    {
        var g = Graybox.Group(parent, "HallDebris");
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Clear(float x, float z) => !(Mathf.Abs(x) < 8.5f && z > 73f && z < 97f) && !(Mathf.Abs(x) < HoleHalf + 0.5f && Mathf.Abs(z - RotundaZ) < HoleHalf + 0.5f);
        string[] books = { Dungeon + "Book_09.prefab", Olive + "Book-closed 10.prefab", Olive + "Book-closed 12.prefab", Olive + "Book-closed 14.prefab", Olive + "Book-open 2.prefab" };
        // Torn runner down the hall, in pieces.
        for (float z = 70f; z < 118f; z += R(4f, 7f))
        {
            if (z > 73f && z < 97f) { z = 97f; continue; }
            var rug = Box(g, "RugPiece", new Vector3(R(-0.4f, 0.4f), FloorTop + 0.01f, z), new Vector3(3f, 0.02f, R(2f, 4f)), rugMat);
            rug.transform.rotation = Quaternion.Euler(0f, R(-12f, 12f), 0f);
        }
        for (int i = 0; i < 35; i++)   // soot / scorch marks from the spells
        {
            float x = R(-30f, 30f), z = R(HallZ0 + 3f, 118f);
            bool up = Mathf.Abs(x) > BalconyIn + 0.5f && rng.NextDouble() < 0.3;
            var s = Box(g, "Scorch", new Vector3(x, (up ? BalconyY : FloorTop) + 0.012f, z), new Vector3(R(1f, 3.5f), 0.02f, R(1f, 3.5f)), sootMat);
            s.transform.rotation = Quaternion.Euler(0f, R(0f, 360f), 0f);
        }
        for (int i = 0; i < 70; i++)    // rubble chunks fallen from the ceiling
        {
            float x = R(-30f, 30f), z = R(HallZ0 + 3f, 118f);
            if (!Clear(x, z)) continue;
            bool up = Mathf.Abs(x) > BalconyIn + 0.5f && rng.NextDouble() < 0.4;
            float size = R(0.2f, 0.9f);
            var c = Box(g, "Rubble", new Vector3(x, (up ? BalconyY : FloorTop) + size * 0.3f, z), Vector3.one * size, rng.NextDouble() < 0.8 ? stoneMat : roofMat);
            c.transform.rotation = Quaternion.Euler(R(0f, 360f), R(0f, 360f), R(0f, 360f));
        }
        var piles = new List<Vector3>();
        for (int i = 0; i < 15; i++) piles.Add(new Vector3(R(-30f, 30f), 0f, R(HallZ0 + 3f, 118f)));
        for (int i = 0; i < 280; i++)   // kept light: more reads as clutter
        {
            var p = i < 170 ? piles[i % piles.Count] + new Vector3(R(-1.8f, 1.8f), 0f, R(-1.8f, 1.8f)) : new Vector3(R(-30f, 30f), 0f, R(HallZ0 + 3f, 118f));
            if (!Clear(p.x, p.z)) continue;
            bool up = Mathf.Abs(p.x) > BalconyIn + 0.5f && rng.NextDouble() < 0.35;
            p.y = (up ? BalconyY : FloorTop) + 0.02f + (i < 170 ? R(0f, 0.3f) : 0f);
            string k = books[rng.Next(books.Length)];
            var b = Prop(g, k, p, R(0f, 360f), k.Contains("Book_09") ? R(2.6f, 3.4f) : R(1.8f, 2.4f));
            if (b != null && rng.NextDouble() < 0.6) b.transform.rotation *= Quaternion.Euler(R(-40f, 40f), 0f, R(-45f, 45f));
        }
        MergeByMaterial(g, 30f);
    }

    // ------------------------------------------------------------------ the door
    /// <summary>The giant metal door in the back wall at balcony level: riveted steel leaves in a stone frame.</summary>
    private static GameObject MetalDoor(Transform parent)
    {
        const float width = 8f, height = DoorTop - BalconyY, z = LibLength - 0.4f;
        var door = BuildGateDoor(parent, z, width, height, doorMetalMat);
        door.name = "MetalDoor";
        door.GetComponent<GateDoor>().duration = 4f;   // heavy
        foreach (var hinge in new[] { door.GetComponent<GateDoor>().leftHinge, door.GetComponent<GateDoor>().rightHinge })
        {
            float cx = hinge.position.x * 0.5f;
            for (float y = 0.8f; y < height - 0.4f; y += 1.1f)
                for (float x = -1.5f; x <= 1.5f; x += 1f)
                {
                    var rivet = Box(hinge, "Rivet", new Vector3(cx + x, y, z - 0.47f), new Vector3(0.14f, 0.14f, 0.1f), ironMat);
                    Object.DestroyImmediate(rivet.GetComponent<Collider>());
                    rivet.isStatic = false;
                }
        }
        door.transform.position += Vector3.up * BalconyY;
        var g = Graybox.Group(parent, "DoorFrame");
        for (int s = -1; s <= 1; s += 2)
        {
            Box(g, "Jamb", new Vector3(s * (width * 0.5f + 0.6f), BalconyY + height * 0.5f, LibLength - 0.5f), new Vector3(1.2f, height, 1f), stoneMat);
        }
        Box(g, "Lintel", new Vector3(0f, DoorTop + 0.5f, LibLength - 0.5f), new Vector3(width + 2.4f, 1f, 1f), stoneMat);
        Prop(g, Dungeon + "WallDecor_05.prefab", new Vector3(0f, DoorTop - 0.2f, LibLength - 1.05f), 180f, 1.6f);
        PointLight(g, "DoorGlow", new Vector3(0f, BalconyY + 4f, LibLength - 4f), new Color(1f, 0.3f, 0.2f), 12f, 1.6f, true, 0.15f);
        return door;
    }

    /// <summary>
    /// A giant iron brazier on a stone plinth beside the metal door (side -1 left, 1 right). Unlit until the hall is
    /// cleared; then Ignite bursts it into flame with a bright light, so it's obvious the way is open.
    /// </summary>
    private static GameObject GiantTorch(Transform parent, int side)
    {
        var g = Graybox.Group(parent, side < 0 ? "GiantTorch_L" : "GiantTorch_R");
        Vector3 b = new Vector3(side * 6.8f, BalconyY, LibLength - 2.4f);
        Box(g, "Plinth", b + Vector3.up * 0.4f, new Vector3(1.6f, 0.8f, 1.6f), stoneMat);
        Box(g, "Pole", b + Vector3.up * 2.6f, new Vector3(0.4f, 3.6f, 0.4f), ironMat);
        Box(g, "Bowl", b + Vector3.up * 4.6f, new Vector3(1.8f, 0.6f, 1.8f), ironMat);
        Box(g, "Rim", b + Vector3.up * 4.95f, new Vector3(2.1f, 0.15f, 2.1f), ironMat);
        Box(g, "Coals", b + Vector3.up * 4.92f, new Vector3(1.5f, 0.1f, 1.5f), sootMat);
        foreach (var c in g.GetComponentsInChildren<Collider>()) if (c.name != "Plinth" && c.name != "Pole") Object.DestroyImmediate(c);

        var flame = Graybox.Group(g, "Flame");
        var core = Mat("L3_FlameCore", new Color(1f, 0.95f, 0.6f));
        core.EnableKeyword("_EMISSION");
        core.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.4f) * 3f);
        core.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        UnityEditor.EditorUtility.SetDirty(core);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(core);
        // Low-poly flame: cubes standing on a corner (orange), a bright yellow core, smaller licks around and above.
        void Lick(Vector3 pos, float size, float yaw, Material m)
        {
            var f = Box(flame, "Flame", pos, Vector3.one * size, m);
            f.transform.rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(45f, 0f, 35.26f);   // a corner points up
            Object.DestroyImmediate(f.GetComponent<Collider>());
            f.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        const string Fx = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
        Lick(b + Vector3.up * 5.7f, 1.3f, 0f, flameMat);
        Lick(b + Vector3.up * 5.85f, 0.9f, 25f, core);
        Lick(b + Vector3.up * 6.75f, 0.75f, 50f, flameMat);
        Lick(b + Vector3.up * 7.4f, 0.4f, 10f, core);
        for (int i = 0; i < 4; i++)
            Lick(b + Vector3.up * (5.4f + (i % 2) * 0.3f) + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 0.7f, 0.55f, i * 40f, flameMat);
        Prop(flame, Fx + "Sparks/Sparks yellow.prefab", b + Vector3.up * 5.5f, 0f, 1.5f);
        Prop(flame, Fx + "Hits and explosions/Explosion.prefab", b + Vector3.up * 5.2f, 0f, 1.5f);   // plays once as it lights
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;

        var light = PointLight(g, "TorchGlow", b + Vector3.up * 6f, new Color(1f, 0.62f, 0.28f), 30f, 5f, true, 0f);
        var ignite = g.gameObject.AddComponent<Ignite>();
        ignite.flame = flame.gameObject;
        ignite.glow = light;
        ignite.intensity = 5f;
        flame.gameObject.SetActive(false);
        light.enabled = false;
        return g.gameObject;
    }

    /// <summary>Behind the door: a short stone corridor (the boss room goes here later) with the exit portal.</summary>
    private static GameObject DoorCorridor(Transform parent)
    {
        var g = Graybox.Group(parent, "C_DoorCorridor");
        const float z0 = LibLength + 2f, z1 = LibLength + 20f;
        Slab(g, "CorridorFloor", -5f, 5f, LibLength, z1, BalconyY, 1f, stoneMat);
        Slab(g, "CorridorCeiling", -5f, 5f, LibLength, z1, DoorTop + 1f, 1f, stoneMat);
        for (int s = -1; s <= 1; s += 2)
            Box(g, "CorridorWall", new Vector3(s * 5.5f, (BalconyY + DoorTop + 1f) * 0.5f, (z0 + z1) * 0.5f), new Vector3(1f, DoorTop + 1f - BalconyY, z1 - z0), stoneMat);
        Box(g, "CorridorEnd", new Vector3(0f, (BalconyY + DoorTop + 1f) * 0.5f, z1 + 0.5f), new Vector3(12f, DoorTop + 1f - BalconyY, 1f), stoneMat);
        PointLight(g, "CorridorLight", new Vector3(0f, DoorTop - 1f, z0 + 9f), new Color(1f, 0.35f, 0.25f), 14f, 1.4f, false, 0.2f);
        return Portal(g, new Vector3(0f, BalconyY, z0 + 12f), 180f);
    }
}
