using UnityEngine;

/// <summary>
/// Little animated effects drawn inside a spell's hotbar slot (IMGUI, clipped to the slot; nothing in the world):
/// fireball flames, frost creeping in from the edges, crackling lightning, a purple arcane glow, a red nova blast.
/// The kind comes from the spell's cast gesture (one per spell). intensity 0..1: low while selected, 1 just after a cast.
/// </summary>
public static class HotbarFx
{
    private static Texture2D glow;

    public static void Draw(Rect slot, CastGesture kind, float intensity, float time)
    {
        if (intensity <= 0.01f || Event.current.type != EventType.Repaint) return;
        GUI.BeginGroup(slot);   // clip to the slot
        var r = new Rect(0f, 0f, slot.width, slot.height);
        switch (kind)
        {
            case CastGesture.Thrust: Fire(r, intensity, time); break;
            case CastGesture.Sweep: Frost(r, intensity, time); break;
            case CastGesture.Point: Lightning(r, intensity, time); break;
            case CastGesture.Flurry: Arcane(r, intensity, time); break;
            case CastGesture.Slam: Nova(r, intensity, time); break;
        }
        GUI.EndGroup();
        GUI.color = Color.white;
    }

    // Flames licking up from the bottom of the slot.
    private static void Fire(Rect r, float k, float t)
    {
        Blob(new Vector2(r.center.x, r.yMax), r.width * 1.1f, r.height * 0.7f, new Color(1f, 0.35f, 0.05f, 0.45f * k));
        for (int i = 0; i < 14; i++)
        {
            float phase = Mathf.Repeat(t * (1.1f + Hash(i) * 0.8f) + Hash(i + 50), 1f);
            float x = r.width * (0.05f + 0.9f * Hash(i + 7)) + Mathf.Sin(t * 6f + i) * r.width * 0.02f;
            float y = r.yMax - phase * r.height * 0.85f;
            float size = r.height * 0.32f * (1f - phase * 0.7f);
            Color c = Color.Lerp(new Color(1f, 0.85f, 0.3f), new Color(1f, 0.2f, 0.05f), phase);
            c.a = (1f - phase) * 0.8f * k;
            Blob(new Vector2(x, y), size * 0.7f, size, c);
        }
    }

    // Frost creeping in from the edges, ice shards in the corners, twinkles.
    private static void Frost(Rect r, float k, float t)
    {
        var ice = new Color(0.75f, 0.92f, 1f);
        float edge = r.height * 0.22f;
        Color e = ice; e.a = 0.35f * k;
        GUI.color = e;
        GUI.DrawTexture(new Rect(0f, 0f, r.width, edge * 0.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, r.yMax - edge * 0.5f, r.width, edge * 0.5f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, edge * 0.5f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax - edge * 0.5f, 0f, edge * 0.5f, r.height), Texture2D.whiteTexture);
        for (int i = 0; i < 4; i++)   // corner shards
        {
            Vector2 corner = new Vector2(i % 2 == 0 ? 0f : r.width, i < 2 ? 0f : r.height);
            for (int j = 0; j < 3; j++)
            {
                float ang = 45f + 90f * i + (j - 1) * 25f;
                Color s = ice; s.a = 0.7f * k;
                Line(corner, ang, edge * (1.6f - j * 0.3f), 3f, s);
            }
        }
        for (int i = 0; i < 8; i++)   // twinkles
        {
            float a = Mathf.Clamp01(Mathf.Sin(t * 3f + i * 1.7f)) * k;
            Color s = Color.white; s.a = a * 0.9f;
            Blob(new Vector2(r.width * Hash(i + 20), r.height * Hash(i + 40)), 10f, 10f, s);
        }
    }

    // Two jagged bolts across the slot, re-drawn every few frames so they crackle.
    private static void Lightning(Rect r, float k, float t)
    {
        int seed = Mathf.FloorToInt(t / 0.07f);
        var col = new Color(0.65f, 0.85f, 1f, 0.95f * k);
        Blob(r.center, r.width, r.height, new Color(0.4f, 0.6f, 1f, 0.25f * k));
        for (int b = 0; b < 2; b++)
        {
            if (Hash(seed * 3 + b) > 0.35f + 0.6f * k) continue;   // flicker on and off
            Vector2 p = new Vector2(0f, r.height * (0.25f + 0.5f * Hash(seed + b * 13)));
            for (int s = 0; s < 6; s++)
            {
                Vector2 q = new Vector2(r.width * (s + 1) / 6f, Mathf.Clamp(p.y + (Hash(seed * 7 + s + b * 31) - 0.5f) * r.height * 0.6f, 4f, r.height - 4f));
                Segment(p, q, 3f, col);
                Segment(p, q, 1.2f, new Color(1f, 1f, 1f, k));
                p = q;
            }
        }
    }

    // Pulsing purple glow and border.
    private static void Arcane(Rect r, float k, float t)
    {
        float pulse = 0.65f + 0.35f * Mathf.Sin(t * 5f);
        var purple = new Color(0.75f, 0.3f, 1f);
        Blob(r.center, r.width * 1.2f, r.height * 1.3f, new Color(purple.r, purple.g, purple.b, 0.45f * k * pulse));
        Color b = purple; b.a = 0.9f * k * pulse;
        GUI.color = b;
        const float w = 3f;
        GUI.DrawTexture(new Rect(0f, 0f, r.width, w), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, r.height - w, r.width, w), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(0f, 0f, w, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.width - w, 0f, w, r.height), Texture2D.whiteTexture);
        for (int i = 0; i < 5; i++)   // motes drifting up
        {
            float ph = Mathf.Repeat(t * 0.6f + Hash(i + 3), 1f);
            Blob(new Vector2(r.width * Hash(i + 9), r.height * (1f - ph)), 9f, 9f, new Color(1f, 0.7f, 1f, (1f - ph) * k));
        }
    }

    // Red glow with a shockwave ring bursting out from the centre.
    private static void Nova(Rect r, float k, float t)
    {
        var red = new Color(1f, 0.15f, 0.1f);
        Blob(r.center, r.width * 1.1f, r.height * 1.2f, new Color(red.r, red.g, red.b, 0.5f * k * (0.75f + 0.25f * Mathf.Sin(t * 4f))));
        float ph = Mathf.Repeat(t * 1.25f, 1f);
        float size = r.width * 1.3f * ph;
        GUI.color = new Color(1f, 0.45f, 0.3f, (1f - ph) * k);
        GUI.DrawTexture(new Rect(r.center.x - size * 0.5f, r.center.y - size * 0.5f, size, size), CrosshairRing.Texture);
    }

    // ------------------------------------------------------------------ primitives
    private static float Hash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.5453f, 1f);

    private static void Blob(Vector2 c, float w, float h, Color col)
    {
        GUI.color = col;
        GUI.DrawTexture(new Rect(c.x - w * 0.5f, c.y - h * 0.5f, w, h), Glow());
    }

    private static void Line(Vector2 from, float angleDeg, float length, float width, Color col)
    {
        Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
        Segment(from, from + dir * length, width, col);
    }

    private static void Segment(Vector2 a, Vector2 b, float width, Color col)
    {
        Matrix4x4 m = GUI.matrix;
        Vector2 d = b - a;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, a);
        GUI.color = col;
        GUI.DrawTexture(new Rect(a.x, a.y - width * 0.5f, d.magnitude, width), Texture2D.whiteTexture);
        GUI.matrix = m;
    }

    // Soft round blob, made once.
    private static Texture2D Glow()
    {
        if (glow != null) return glow;
        const int size = 64;
        glow = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HotbarGlow" };
        var px = new Color32[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d); a *= a;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        glow.SetPixels32(px);
        glow.Apply();
        return glow;
    }
}

/// <summary>The ring texture shared by the crosshair and the nova hotbar effect.</summary>
public static class CrosshairRing
{
    private static Texture2D ring;

    public static Texture2D Texture
    {
        get
        {
            if (ring != null) return ring;
            const int size = 128;
            ring = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "CrosshairRing" };
            var px = new Color32[size * size];
            float c = (size - 1) * 0.5f, outer = c - 1f, thickness = 7f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - (outer - thickness * 0.5f)) / (thickness * 0.5f) + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            ring.SetPixels32(px);
            ring.Apply();
            return ring;
        }
    }
}
