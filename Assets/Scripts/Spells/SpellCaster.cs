using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets the player cast spells. Hold Mouse 1 (left click) to cast the selected spell whenever it's off cooldown.
/// Aims at whatever is under the crosshair, firing from a hand point so shots line up with the screen center.
/// Number keys 1-9 or the mouse wheel select among unlocked spells.
/// Draws the crosshair and a spell hotbar (bottom centre) showing each spell's cooldown.
/// </summary>
[DefaultExecutionOrder(50)]   // casts in LateUpdate, after the camera has turned this frame, so shots match the crosshair
public class SpellCaster : MonoBehaviour
{
    [SerializeField] private List<Spell> spells = new List<Spell>();
    [SerializeField] private int unlockedCount = 1;
    [SerializeField] private Transform aimCamera;
    [SerializeField] private Transform castPoint;          // "hand" position, child of the camera
    [SerializeField] private float maxAimDistance = 200f;
    [SerializeField] private bool showCrosshair = true;

    private readonly Dictionary<Spell, float> readyTime = new Dictionary<Spell, float>();
    private int selected;

    /// <summary>Raised after a spell is successfully cast (used for hand/book animation, audio, etc).</summary>
    public event System.Action<Spell> Cast;

    private int shownSelected;
    private float switchedAt = -10f;
    private GUIStyle switchStyle, nameStyle, keyStyle, cdStyle;
    private Spell windingSpell;
    private float windEnd;
    private Spell pendingRelease;

    /// <summary>True while a spell with a cast time is winding up.</summary>
    public bool IsCasting => windingSpell != null && Time.time < windEnd;
    /// <summary>Move speed multiplier for the controller (slowed while winding up).</summary>
    public float MoveMultiplier => IsCasting ? windingSpell.castMoveMultiplier : 1f;

    /// <summary>Raised when the selected spell slot changes (the book swap listens).</summary>
    public event System.Action<int> SpellSelected;
    public int SelectedIndex => selected;

    public Spell CurrentSpell => spells.Count > 0 ? spells[selected] : null;
    public int UnlockedCount => Mathf.Min(unlockedCount, spells.Count);

    private void Awake()
    {
        if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.transform;
        if (castPoint == null) castPoint = aimCamera;
    }

    private void Update()
    {
        for (int i = 0; i < UnlockedCount && i < 9; i++)
            if (Input.GetKeyDown(KeyCode.Alpha1 + i)) selected = i;
        float wheel = Input.mouseScrollDelta.y;
        if (UnlockedCount > 1 && Mathf.Abs(wheel) > 0.01f)
            selected = (selected + (wheel < 0f ? 1 : -1) + UnlockedCount) % UnlockedCount;
        if (selected >= UnlockedCount) selected = 0;
        if (selected != shownSelected) { shownSelected = selected; switchedAt = Time.unscaledTime; SpellSelected?.Invoke(selected); }

    }

    // Aim is read here, after every Update: the controller applies this frame's mouse look at the end of its
    // Update, so casting from LateUpdate always fires along this frame's view.
    private void LateUpdate()
    {
        if (pendingRelease != null)
        {
            var spell = pendingRelease;
            pendingRelease = null;
            windingSpell = null;
            Vector3 origin = castPoint.position;
            spell.Cast(this, origin, AimDirection(origin));
        }
        if (Cursor.lockState != CursorLockMode.Locked || GameFlow.Paused) return;   // first click just re-locks the cursor
        if (Input.GetMouseButton(0)) TryCast();
    }

    public bool TryCast()
    {
        Spell spell = CurrentSpell;
        if (spell == null || CooldownRemaining(spell) > 0f || IsCasting) return false;

        if (spell.castTime > 0f)
        {
            // Wind up: gesture now, the spell goes off when the cast time is over.
            windingSpell = spell;
            windEnd = Time.time + spell.castTime;
            readyTime[spell] = windEnd + spell.cooldown;
            spell.BeginCast(this);
            Cast?.Invoke(spell);
            StartCoroutine(Release(spell));
            return true;
        }

        Vector3 origin = castPoint.position;
        Vector3 dir = AimDirection(origin);
        spell.Cast(this, origin, dir);
        readyTime[spell] = Time.time + spell.cooldown;
        Cast?.Invoke(spell);
        return true;
    }

    private System.Collections.IEnumerator Release(Spell spell)
    {
        yield return new WaitForSeconds(spell.castTime);
        pendingRelease = spell;   // fired in LateUpdate with this frame's aim
    }

    private Vector3 AimDirection(Vector3 origin) => Aim.DirectionFrom(origin, aimCamera, transform, maxAimDistance);

    public float CooldownRemaining(Spell spell)
    {
        return readyTime.TryGetValue(spell, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;
    }

    public void SetCastPoint(Transform point) => castPoint = point != null ? point : aimCamera;

    private void OnGUI()
    {
        if (!showCrosshair || GameFlow.Paused) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        Spell spell = CurrentSpell;
        bool ready = spell == null || CooldownRemaining(spell) <= 0f;
        GUI.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);

        DrawHotbar();
        DrawSwitchName(cx, cy);
    }

    // The new spell's name under the crosshair for a moment after switching, then fades.
    private void DrawSwitchName(float cx, float cy)
    {
        Spell s = CurrentSpell;
        float a = 1f - Mathf.Clamp01((Time.unscaledTime - switchedAt - 0.25f) / 0.35f);
        if (s == null || a <= 0f) return;
        float ui = Screen.height / 1080f;
        if (switchStyle == null) switchStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        switchStyle.fontSize = Mathf.RoundToInt(22 * ui);
        var r = new Rect(cx - 300f * ui, cy + 30f * ui, 600f * ui, 34f * ui);
        switchStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), s.displayName, switchStyle);
        switchStyle.normal.textColor = new Color(s.uiColor.r, s.uiColor.g, s.uiColor.b, a);
        GUI.Label(r, s.displayName, switchStyle);
    }

    // One slot per unlocked spell: number, name, a coloured strip, and a dark overlay that drains as the
    // cooldown runs out. The selected slot is raised and outlined.
    private void DrawHotbar()
    {
        int count = UnlockedCount;
        if (count == 0) return;
        float ui = Screen.height / 1080f;
        float w = 235f * ui, h = 105f * ui, gap = 12f * ui;
        float x0 = (Screen.width - (count * w + (count - 1) * gap)) * 0.5f;
        float y = Screen.height - h - 24f * ui;

        if (nameStyle == null)   // built once: OnGUI runs several times a frame
        {
            nameStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
            keyStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, alignment = TextAnchor.UpperLeft };
            cdStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        nameStyle.fontSize = Mathf.RoundToInt(28 * ui);
        keyStyle.fontSize = Mathf.RoundToInt(22 * ui);
        cdStyle.fontSize = Mathf.RoundToInt(36 * ui);

        for (int i = 0; i < count; i++)
        {
            Spell s = spells[i];
            bool sel = i == selected;
            var r = new Rect(x0 + i * (w + gap), y - (sel ? 12f * ui : 0f), w, h);

            if (sel) { GUI.color = s.uiColor; GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture); }
            GUI.color = new Color(0f, 0f, 0f, sel ? 0.85f : 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = s.uiColor;
            GUI.DrawTexture(new Rect(r.x, r.yMax - 6f * ui, r.width, 6f * ui), Texture2D.whiteTexture);

            float cd = CooldownRemaining(s);
            if (cd > 0f && s.cooldown > 0f)
            {
                float frac = Mathf.Clamp01(cd / s.cooldown);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * frac), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            keyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(r.x + 6 * ui, r.y + 2 * ui, 40 * ui, 32 * ui), (i + 1).ToString(), keyStyle);   // rect must fit the font or it's clipped
            nameStyle.normal.textColor = sel ? s.uiColor : new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(r.x, r.y, r.width, r.height - 6f * ui), s.displayName, nameStyle);
            if (cd > 0.05f && s.cooldown >= 1.5f)
            {
                cdStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(r.x, r.y - 6f * ui, r.width, r.height), cd.ToString("0.0"), cdStyle);
            }
        }
        GUI.color = Color.white;
    }
}
