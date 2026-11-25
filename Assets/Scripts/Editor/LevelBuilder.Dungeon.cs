using UnityEditor;
using UnityEngine;

/// <summary>
/// Dungeon dressing for the tutorial (TutorialBuilder calls these): an enclosed stone hall over a dark pit,
/// an evenly lit glowing ceiling, wall torches, and wall-run panels that look different
/// from every other wall (teal wood with glowing rune strips).
/// </summary>
public static partial class LevelBuilder
{
    public const float DungeonHalfW = 12f, DungeonCeiling = 18f, DungeonPit = -20f;
    private static Material runeMat;

    public struct DungeonMats { public Material platform, stone, wallRun, rune; }

    public static DungeonMats TutorialMaterials()
    {
        RuneMat();
        return new DungeonMats
        {
            platform = Mat("TUT_PlatformRock", new Color(0.46f, 0.43f, 0.4f)),
            stone = Mat("TUT_DungeonStone", new Color(0.42f, 0.4f, 0.38f)),
            wallRun = Mat("TUT_WallRunWood", new Color(0.25f, 0.6f, 0.75f)),
            rune = runeMat,
        };
    }

    /// <summary>Walls, pit floor, skylit ceiling, beams, torches and light from above for z0..z1.</summary>
    public static void TutorialShell(Transform parent, float z0, float z1, DungeonMats m)
    {
        var g = Graybox.Group(parent, "DungeonShell");
        var pitMat = Mat("TUT_PitRock", new Color(0.2f, 0.19f, 0.18f));
        var ceilMat = Mat("TUT_CeilingStone", new Color(0.3f, 0.28f, 0.27f));
        var beamMat = Mat("TUT_BeamWood", new Color(0.3f, 0.2f, 0.12f));
        float W = DungeonHalfW, H = DungeonCeiling, P = DungeonPit;

        Slab(g, "PitFloor", -W, W, z0, z1, P, 1f, pitMat);
        // Side walls, tiled so each piece picks up its nearest torches; stone pilasters break them up.
        for (float z = z0; z < z1; z += 16f)
        {
            float len = Mathf.Min(16f, z1 - z);
            foreach (int s in new[] { -1, 1 })
            {
                Box(g, "Wall", new Vector3(s * (W + 0.5f), (P + H) * 0.5f, z + len * 0.5f), new Vector3(1f, H - P, len), m.stone);
                Box(g, "Pilaster", new Vector3(s * (W - 0.3f), (P + H) * 0.5f, z), new Vector3(0.6f, H - P, 1.4f), m.stone);
            }
        }
        foreach (float z in new[] { z0 - 0.5f, z1 + 0.5f })
            Box(g, "EndWall", new Vector3(0f, (P + H) * 0.5f, z), new Vector3(W * 2f + 2f, H - P, 1f), m.stone);

        // Ceiling: solid stone, wooden beams every 6 m with a glowing panel between each pair, so the whole
        // ceiling reads as the light source. A shadowless straight-down sun lights everything evenly.
        SetupDungeonLight();
        var glowMat = Mat("TUT_CeilingGlow", new Color(1f, 0.92f, 0.78f));
        glowMat.EnableKeyword("_EMISSION");
        glowMat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.55f) * 0.9f);
        glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(glowMat);
        AssetDatabase.SaveAssetIfDirty(glowMat);
        Slab(g, "Ceiling", -W, W, z0, z1, H + 1f, 1f, ceilMat);
        for (float z = z0; z < z1; z += 6f)
        {
            Box(g, "Beam", new Vector3(0f, H - 0.4f, z), new Vector3(W * 2f, 0.8f, 0.6f), beamMat);
            float len = Mathf.Min(6f, z1 - z) - 1.2f;
            if (len > 0.5f)
            {
                var panel = Box(g, "CeilingLight", new Vector3(0f, H - 0.05f, z + 0.6f + len * 0.5f), new Vector3(W * 2f - 2f, 0.1f, len), glowMat);
                Object.DestroyImmediate(panel.GetComponent<Collider>());
                panel.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // Torches on both walls, above the highest floor.
        for (float z = z0 + 6f; z < z1 - 2f; z += 12f)
            foreach (int s in new[] { -1, 1 })
                WallTorch(g, new Vector3(s * (W - 0.5f), 7f, z), s > 0 ? -90f : 90f);

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.18f, 0.16f, 0.14f);
        RenderSettings.fogDensity = 0.008f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.55f, 0.5f, 0.45f);
        RenderSettings.skybox = null;
    }

    // The scene's sun, straight down with no shadows: even light everywhere, as if from the glowing ceiling.
    private static Light SetupDungeonLight()
    {
        var go = GameObject.Find("Directional Light");
        if (go == null) go = new GameObject("Directional Light", typeof(Light));
        go.name = "Ceiling Light";
        go.transform.rotation = Quaternion.Euler(80f, 20f, 0f);
        var l = go.GetComponent<Light>();
        l.type = LightType.Directional;
        l.color = new Color(1f, 0.93f, 0.8f);
        l.intensity = 0.8f;
        l.shadows = LightShadows.None;
        return l;
    }

    /// <summary>A wall-runnable panel: teal wood with glowing rune strips along both faces so it reads as "run on me".</summary>
    public static GameObject RunePanel(Transform parent, string name, Vector3 center, Vector3 size, DungeonMats m)
    {
        var wall = Graybox.Box(parent, name, center, size, m.wallRun, wallRun: true);
        float half = size.x * 0.5f + 0.03f;
        foreach (int s in new[] { -1, 1 })
        {
            foreach (float y in new[] { -0.4f, 0.4f })   // bottom and top bands (fractions of height)
                RuneBit(wall.transform.parent, name, center + new Vector3(s * half, y * size.y, 0f), new Vector3(0.06f, 0.4f, size.z * 0.96f), 0f);
            // A row of diamond glyphs at running height.
            for (float z = -size.z * 0.5f + 1.5f; z <= size.z * 0.5f - 1.4f; z += 2.5f)
                RuneBit(wall.transform.parent, name, center + new Vector3(s * half, 0f, z), new Vector3(0.06f, 0.6f, 0.6f), 45f);
        }
        PointLight(wall.transform.parent, name + "_Glow", center + Vector3.up * size.y * 0.2f, new Color(0.3f, 0.85f, 1f), 9f, 0.8f, false, 0f);
        return wall;
    }

    // glow 1.5 = the tutorial's runes; dimmer levels get their own material so the tutorial is untouched.
    private static Material RuneMat(float glow = 1.5f)
    {
        runeMat = glow >= 1.5f ? Mat("TUT_RuneGlow", new Color(0.3f, 0.9f, 1f)) : Mat("RuneGlow_Dim", new Color(0.12f, 0.4f, 0.48f));
        runeMat.EnableKeyword("_EMISSION");
        runeMat.SetColor("_EmissionColor", new Color(0.15f, 0.7f, 1f) * glow);
        runeMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        runeMat.enableInstancing = true;   // hundreds of identical strips per level
        EditorUtility.SetDirty(runeMat);
        AssetDatabase.SaveAssetIfDirty(runeMat);
        return runeMat;
    }

    /// <summary>
    /// Tutorial-style rune strips on every WallRun-tagged cube under root (bands top and bottom, diamonds at the
    /// middle), so wall-runnable surfaces read the same in every level. Faces pressed against something get none.
    /// Re-runnable: replaces the "RuneTrim" group. Safe on hand-edited scenes (only adds that group).
    /// </summary>
    public static void RuneTrim(Transform root, float glow = 1.5f)
    {
        var old = root.Find("RuneTrim");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        RuneMat(glow);
        Physics.SyncTransforms();
        var g = Graybox.Group(root, "RuneTrim");
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            if (!t.CompareTag(Graybox.WallRunTag)) continue;
            var mf = t.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Cube") continue;
            Vector3 s = t.lossyScale;
            bool thinX = s.x <= s.z;
            Vector3 thin = thinX ? Vector3.right : Vector3.forward, along = thinX ? Vector3.forward : Vector3.right;
            float L = thinX ? s.z : s.x, H = s.y, half = (thinX ? s.x : s.z) * 0.5f + 0.03f;
            Quaternion rollQ = Quaternion.AngleAxis(45f, thin);
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 n = t.rotation * thin * side, face = t.position + n * half;
                if (Physics.Raycast(face - n * 0.02f, n, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;   // flush against a wall
                foreach (float y in new[] { -0.4f, 0.4f })
                    RuneStrip(g, face + t.rotation * (Vector3.up * y * H), t.rotation, thin * 0.06f + Vector3.up * 0.3f + along * L * 0.96f);
                for (float a = -L * 0.5f + 1.5f; a <= L * 0.5f - 1.4f; a += 2.5f)
                    RuneStrip(g, face + t.rotation * (along * a), t.rotation * rollQ, thin * 0.06f + (Vector3.up + along) * 0.45f);
            }
        }
    }

    private static void RuneStrip(Transform g, Vector3 pos, Quaternion rot, Vector3 size)
    {
        var bit = Graybox.Box(g, "Rune", pos, size, runeMat);
        bit.transform.rotation = rot;
        Object.DestroyImmediate(bit.GetComponent<Collider>());
        bit.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static void RuneBit(Transform parent, string name, Vector3 pos, Vector3 size, float roll)
    {
        var bit = Graybox.Box(parent, name + "_Rune", pos, size, runeMat);
        bit.transform.rotation = Quaternion.Euler(roll, 0f, 0f);
        Object.DestroyImmediate(bit.GetComponent<Collider>());   // never get between the player and the wall
        bit.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
