using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tiling surface textures for the graybox levels. The asset packs only ship colour atlases, so these are
/// generated (seamless noise / bricks / planks, light grey so the material colour still tints them) and
/// drawn with the world-space OxfordRunner/Triplanar shader, so any box tiles evenly.
/// LevelBuilder.Mat() calls Apply() for every level material.
/// </summary>
public static class LevelTextures
{
    private const string Dir = "Assets/Materials/Levels/Textures/";
    private const int Size = 256;

    /// <summary>Gives a level material a texture based on its name. Returns false if none fits.</summary>
    public static bool Apply(Material mat, string name)
    {
        string n = name.ToLowerInvariant();
        string kind; float scale;
        if (n.Contains("portal") || n.Contains("beacon") || n.Contains("gate.") || n.Contains("areagate")) return false;
        if (n.Contains("grass") || n.Contains("ground") || n.Contains("field")) { kind = "Grass"; scale = 6f; }
        else if (n.Contains("cliff") || n.Contains("dirt")) { kind = "Dirt"; scale = 5f; }
        else if (n.Contains("rock")) { kind = "Rock"; scale = 3f; }
        else if (n.Contains("roof")) { kind = "Tiles"; scale = 2f; }
        else if (n.Contains("wood") || n.Contains("barricade")) { kind = "Wood"; scale = 2.5f; }
        else if (n.Contains("stone") || n.Contains("castle") || n.Contains("wall") || n.Contains("tower")) { kind = "Bricks"; scale = 3f; }
        else return false;

        var shader = Shader.Find("OxfordRunner/Triplanar");
        var tex = Get(kind);
        if (shader == null || tex == null) return false;
        mat.shader = shader;
        mat.SetTexture("_MainTex", tex);
        mat.SetFloat("_Scale", scale);
        mat.SetFloat("_Glossiness", 0.08f);
        return true;
    }

    private static Texture2D Get(string kind)
    {
        string path = Dir + kind + ".png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        if (!AssetDatabase.IsValidFolder(Dir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Materials/Levels", "Textures");

        var pixels = new Color[Size * Size];
        var rng = new System.Random(kind.GetHashCode());
        float ox = (float)rng.NextDouble() * 100f, oy = (float)rng.NextDouble() * 100f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = (float)x / Size, v = (float)y / Size;
                pixels[y * Size + x] = Grey(Sample(kind, u, v, ox, oy));
            }
        var tex = new Texture2D(Size, Size, TextureFormat.RGB24, true);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Color Grey(float g) { g = Mathf.Clamp01(g); return new Color(g, g, g); }

    // Seamless fractal noise: Perlin sampled on a torus (blend of 4 shifted copies).
    private static float Noise(float u, float v, float freq, float ox, float oy)
    {
        float a = Mathf.PerlinNoise(ox + u * freq, oy + v * freq);
        float b = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + v * freq);
        float c = Mathf.PerlinNoise(ox + u * freq, oy + (v - 1f) * freq);
        float d = Mathf.PerlinNoise(ox + (u - 1f) * freq, oy + (v - 1f) * freq);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    private static float Fbm(float u, float v, float freq, float ox, float oy)
        => Noise(u, v, freq, ox, oy) * 0.55f + Noise(u, v, freq * 2f, ox + 7f, oy + 3f) * 0.3f + Noise(u, v, freq * 4f, ox + 13f, oy + 9f) * 0.15f;

    private static float Sample(string kind, float u, float v, float ox, float oy)
    {
        switch (kind)
        {
            case "Grass":
            {
                float n = Fbm(u, v, 6f, ox, oy);
                float blades = Noise(u, v, 64f, ox + 50f, oy) ;
                return 0.72f + (n - 0.5f) * 0.6f + (blades - 0.5f) * 0.35f;
            }
            case "Dirt":
            {
                float strata = Mathf.Sin((v + Fbm(u, v, 3f, ox, oy) * 0.25f) * Mathf.PI * 2f * 8f) * 0.5f + 0.5f;
                return 0.72f + (Fbm(u, v, 8f, ox, oy) - 0.5f) * 0.4f + (strata - 0.5f) * 0.12f;
            }
            case "Rock":
            {
                float n = Fbm(u, v, 5f, ox, oy);
                float crack = Mathf.Abs(Noise(u, v, 10f, ox + 30f, oy) - 0.5f) < 0.02f ? -0.2f : 0f;
                return 0.8f + (n - 0.5f) * 0.45f + crack;
            }
            case "Wood":
            {
                const int planks = 4;
                float pv = v * planks;
                float seam = Mathf.Abs(pv - Mathf.Round(pv)) < 0.03f ? -0.3f : 0f;
                float grain = Mathf.Sin((u * 3f + Noise(u, v, 4f, ox + Mathf.Floor(pv) * 5f, oy) * 0.6f) * Mathf.PI * 2f * 6f) * 0.06f;
                return 0.8f + grain + (Fbm(u, v, 6f, ox, oy) - 0.5f) * 0.2f + seam;
            }
            case "Tiles":
            {
                const int rows = 8;
                float rv = v * rows, row = Mathf.Floor(rv);
                float ru = u * rows / 2f + (row % 2f) * 0.5f;
                float edge = Mathf.Min(rv - row, Mathf.Abs(ru - Mathf.Round(ru)) * 2f);
                return 0.8f + (edge < 0.08f ? -0.3f : 0f) + (Fbm(u, v, 10f, ox, oy) - 0.5f) * 0.2f - (rv - row) * 0.12f;
            }
            default: // Bricks
            {
                const int rows = 8, cols = 4;
                float rv = v * rows, row = Mathf.Floor(rv);
                float cu = u * cols + (row % 2f) * 0.5f;
                float mortarV = Mathf.Abs(rv - Mathf.Round(rv)) < 0.06f ? 1f : 0f;
                float mortarU = Mathf.Abs(cu - Mathf.Round(cu)) < 0.035f ? 1f : 0f;
                float brick = Noise(Mathf.Floor(cu) / cols, row / rows, 37f, ox, oy);   // per-brick shade
                float g = 0.78f + (brick - 0.5f) * 0.25f + (Fbm(u, v, 12f, ox, oy) - 0.5f) * 0.15f;
                return Mathf.Max(mortarU, mortarV) > 0f ? 0.55f : g;
            }
        }
    }
}
