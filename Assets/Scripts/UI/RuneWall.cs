using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A full-screen close-up of a rune wall that slides past forever: a tiled texture, two glowing bands along the
/// direction of travel and a row of diamonds down the middle, gently pulsing. Horizontal = the view pans right
/// (main menu); vertical = the view drops fast (PortalFall). Runs on unscaled time (works while paused).
/// </summary>
public class RuneWall : MonoBehaviour
{
    public static readonly Color Rune = new Color(0.3f, 0.9f, 1f);

    public float speed = 70f;   // canvas px per second
    private bool vertical;
    private float tilePx, gap;
    private RawImage wall;
    private RectTransform area;
    private readonly List<RectTransform> diamonds = new List<RectTransform>();
    private Image[] glows;

    public static RuneWall Build(Transform canvas, Texture texture, Color tint, bool vertical, float speed, float tilePx = 640f, float gap = 480f)
    {
        var area = MenuUI.FullScreen(canvas, "RuneWall", Color.clear);
        area.GetComponent<Image>().raycastTarget = false;
        var w = area.gameObject.AddComponent<RuneWall>();
        w.area = area;
        w.vertical = vertical;
        w.speed = speed;
        w.tilePx = tilePx;
        w.gap = gap;
        if (texture != null)
        {
            var go = new GameObject("Wall", typeof(RectTransform));
            go.transform.SetParent(area, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            w.wall = go.AddComponent<RawImage>();
            w.wall.texture = texture;
            w.wall.color = tint;
            w.wall.raycastTarget = false;
        }
        var parts = new List<Image>();
        foreach (float off in new[] { 380f, -380f })
        {
            parts.Add(Strip(area, "Halo", off, 70f, vertical));
            parts.Add(Strip(area, "Band", off, 16f, vertical));
        }
        int count = Mathf.CeilToInt(2400f / gap) + 1;   // a wide screen plus one off each end
        for (int i = 0; i < count; i++)
        {
            var halo = MenuUI.Rect(area, "Halo", Vector2.zero, new Vector2(150f, 150f), Rune);
            var d = MenuUI.Rect(halo, "Diamond", Vector2.zero, new Vector2(70f, 70f), Rune);
            halo.localRotation = Quaternion.Euler(0f, 0f, 45f);
            parts.Add(halo.GetComponent<Image>());
            parts.Add(d.GetComponent<Image>());
            w.diamonds.Add(halo);
        }
        foreach (var p in parts) p.raycastTarget = false;
        w.glows = parts.ToArray();
        return w;
    }

    private static Image Strip(RectTransform parent, string name, float offset, float thickness, bool vertical)
    {
        var rt = MenuUI.Rect(parent, name, Vector2.zero, Vector2.zero, Rune);
        rt.anchorMin = vertical ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
        rt.anchorMax = vertical ? new Vector2(0.5f, 1f) : new Vector2(1f, 0.5f);
        rt.anchoredPosition = vertical ? new Vector2(offset * 1.6f, 0f) : new Vector2(0f, offset);   // bands sit wider apart on a falling view
        rt.sizeDelta = vertical ? new Vector2(thickness, 0f) : new Vector2(0f, thickness);
        return rt.GetComponent<Image>();
    }

    private void Update()
    {
        // Wall and diamonds move together; diamonds leaving one edge wrap round to the other, off screen.
        Rect r = area.rect;
        float pan = Time.unscaledTime * speed;
        float along = vertical ? r.height : r.width;
        if (wall != null)
            wall.uvRect = vertical ? new Rect(0f, -pan / tilePx, r.width / tilePx, r.height / tilePx)    // view falls: wall rushes up
                                   : new Rect(pan / tilePx, 0f, r.width / tilePx, r.height / tilePx);   // view pans right: wall slides left
        float loop = diamonds.Count * gap;
        for (int i = 0; i < diamonds.Count; i++)
        {
            float p = Mathf.Repeat(i * gap - pan, loop) - along * 0.5f - gap * 0.5f;
            diamonds[i].anchoredPosition = vertical ? new Vector2(0f, -p) : new Vector2(p, 0f);
        }
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 1.6f);   // the runes breathe
        foreach (var g in glows) g.color = new Color(g.color.r, g.color.g, g.color.b, g.name == "Halo" ? 0.18f * pulse : pulse);
    }
}
