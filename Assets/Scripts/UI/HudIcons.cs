using UnityEngine;

/// <summary>
/// HUD icon sprites drawn in code (white, tinted by the Image): dash = double chevron with speed lines,
/// shield = parry. Plus a plain white sprite for filled bars. Made once, on first use.
/// </summary>
public static class HudIcons
{
    private const int Size = 64;
    private static Sprite dash, shield, white;

    public static Sprite White => white != null ? white : white = Make(_ => 1f, "HudWhite", 4);

    public static Sprite Dash => dash != null ? dash : dash = Make(p =>
    {
        float d = Mathf.Min(Seg(p, 22, 14, 36, 32), Seg(p, 36, 32, 22, 50));        // >
        d = Mathf.Min(d, Mathf.Min(Seg(p, 36, 14, 50, 32), Seg(p, 50, 32, 36, 50)));   // >
        float a = Edge(d, 3.2f);
        float lines = Mathf.Min(Seg(p, 4, 24, 13, 24), Mathf.Min(Seg(p, 2, 32, 15, 32), Seg(p, 4, 40, 13, 40)));   // speed lines
        return Mathf.Max(a, Edge(lines, 1.6f) * 0.8f);
    }, "HudDash");

    public static Sprite Shield => shield != null ? shield : shield = Make(p =>
    {
        // Flat-ish top, straight sides, tapering to a point at the bottom.
        float x = Mathf.Abs(p.x - 31.5f), y = p.y;
        float half = y >= 30f ? 22f : 22f * Mathf.Clamp01((y - 5f) / 25f);
        bool inside = y >= 5f && y <= 56f - x * x * 0.012f && x <= half;
        if (!inside) return 0f;
        float toEdge = Mathf.Min(half - x, 56f - x * x * 0.012f - y, y >= 30f ? 99f : (half - x) * 1.2f);
        return toEdge < 4.5f ? 1f : 0.35f;   // solid rim, see-through middle
    }, "HudShield");

    private static Sprite Make(System.Func<Vector2, float> alpha, string name, int size = Size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(new Vector2(x, y))) * 255));
        tex.SetPixels32(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    // Distance from p to segment (ax, ay)-(bx, by), in pixels.
    private static float Seg(Vector2 p, float ax, float ay, float bx, float by)
    {
        Vector2 a = new Vector2(ax, ay), ab = new Vector2(bx, by) - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * t);
    }

    private static float Edge(float distance, float halfWidth) => Mathf.Clamp01(halfWidth + 0.5f - distance);
}
