using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lock-on for spells with <see cref="ProjectileSpell.lockOn"/> (Arcane Missiles). While such a spell is
/// selected, every enemy in range and in view gets a red [ ] bracket. Hold the crosshair on one and its
/// bracket closes in and turns green: locked on ("LOCKED ON" under it). Missiles fired while locked chase
/// that enemy wherever it goes. The lock holds until the target dies, gets too far, or you lock another.
/// </summary>
[RequireComponent(typeof(SpellCaster))]
public class MissileLockOn : MonoBehaviour
{
    [SerializeField] private Color searchColor = new Color(1f, 0.2f, 0.2f, 0.9f);
    [SerializeField] private Color lockedColor = new Color(0.25f, 1f, 0.35f, 1f);

    private SpellCaster caster;
    private Camera cam;
    private Enemy candidate, locked;
    private float progress, lockedAt;
    private GUIStyle labelStyle;
    private readonly List<Enemy> visible = new List<Enemy>();

    /// <summary>The locked enemy, or null.</summary>
    public IDamageable LockedTarget => locked != null && !locked.IsDead ? locked : null;

    private ProjectileSpell ActiveSpell => caster.CurrentSpell is ProjectileSpell p && p.lockOn ? p : null;

    private void Awake()
    {
        caster = GetComponent<SpellCaster>();
    }

    private void Update()
    {
        if (cam == null) cam = Camera.main;
        var spell = ActiveSpell;
        visible.Clear();
        if (spell == null || cam == null) { candidate = null; locked = null; progress = 0f; return; }

        Vector3 eye = cam.transform.position, fwd = cam.transform.forward;
        Enemy best = null;
        float bestAngle = spell.lockConeAngle;
        foreach (var e in Enemy.All)
        {
            if (e == null || e.IsDead) continue;
            Vector3 c = e.VisualBounds.center;
            Vector3 to = c - eye;
            if (to.magnitude > spell.lockRange || Vector3.Dot(to, fwd) <= 0f) continue;
            if (!Targeting.Clear(eye, c, e.transform, transform)) continue;
            visible.Add(e);
            float angle = Vector3.Angle(fwd, to);
            if (angle < bestAngle) { bestAngle = angle; best = e; }
        }

        if (locked != null && (locked.IsDead || (locked.transform.position - eye).magnitude > spell.lockRange * 1.5f
                               || (locked.lockHoldTime > 0f && Time.time - lockedAt > locked.lockHoldTime)))   // the boss shakes a lock off
            locked = null;

        if (best != null && best != locked)
        {
            if (best != candidate) { candidate = best; progress = 0f; }
            progress += Time.deltaTime / Mathf.Max(0.01f, spell.lockTime * best.lockTimeScale);
            if (progress >= 1f) { locked = best; lockedAt = Time.time; candidate = null; progress = 0f; Sfx.Play(Sfx.Bank.lockOn, 0.5f, 0f); }
        }
        else { candidate = null; progress = 0f; }
    }

    private void OnGUI()
    {
        if (ActiveSpell == null || cam == null || GameFlow.Paused) return;
        float ui = Screen.height / 1080f;
        foreach (var e in visible)
        {
            if (e == null) continue;
            if (!ScreenRect(e, out Rect r)) continue;
            if (e == locked)
            {
                Brackets(r, lockedColor, 3f * ui);
                Label(new Rect(r.center.x - 100f * ui, r.yMax + 4f * ui, 200f * ui, 26f * ui), "LOCKED ON", lockedColor, ui);
            }
            else if (e == candidate)
            {
                float pad = (1f - progress) * 40f * ui;
                var grown = new Rect(r.x - pad, r.y - pad, r.width + pad * 2f, r.height + pad * 2f);
                Brackets(grown, Color.Lerp(searchColor, lockedColor, progress), 2.5f * ui);
            }
            else Brackets(r, searchColor, 2f * ui);
        }
    }

    private bool ScreenRect(Enemy e, out Rect rect)
    {
        Bounds b = e.VisualBounds;
        Vector3 min = new Vector3(float.MaxValue, float.MaxValue), max = new Vector3(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 sp = cam.WorldToScreenPoint(corner);
            if (sp.z <= 0f) { rect = default; return false; }
            min = Vector3.Min(min, sp);
            max = Vector3.Max(max, sp);
        }
        float pad = 6f;
        rect = new Rect(min.x - pad, Screen.height - max.y - pad, max.x - min.x + pad * 2f, max.y - min.y + pad * 2f);
        return true;
    }

    // Four corner brackets: [ ]
    private static void Brackets(Rect r, Color c, float t)
    {
        GUI.color = c;
        float lx = Mathf.Max(8f, r.width * 0.28f), ly = Mathf.Max(8f, r.height * 0.2f);
        var tex = Texture2D.whiteTexture;
        // top-left
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, lx, t), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMin, t, ly), tex);
        // top-right
        GUI.DrawTexture(new Rect(r.xMax - lx, r.yMin, lx, t), tex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMin, t, ly), tex);
        // bottom-left
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - t, lx, t), tex);
        GUI.DrawTexture(new Rect(r.xMin, r.yMax - ly, t, ly), tex);
        // bottom-right
        GUI.DrawTexture(new Rect(r.xMax - lx, r.yMax - t, lx, t), tex);
        GUI.DrawTexture(new Rect(r.xMax - t, r.yMax - ly, t, ly), tex);
        GUI.color = Color.white;
    }

    private void Label(Rect r, string text, Color c, float ui)
    {
        if (labelStyle == null) labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        labelStyle.fontSize = Mathf.RoundToInt(20f * ui);
        labelStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, labelStyle);
        labelStyle.normal.textColor = c;
        GUI.Label(r, text, labelStyle);
    }
}
