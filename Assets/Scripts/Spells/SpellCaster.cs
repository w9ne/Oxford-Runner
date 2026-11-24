using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets the player cast spells. Hold Mouse 1 (left click) to cast the selected spell whenever it's off cooldown.
/// Aims at whatever is under the crosshair, firing from a hand point so shots line up with the screen center.
/// Number keys 1-9 or the mouse wheel select among unlocked spells (spells unlock per stage later via UnlockNext()).
/// Draws the crosshair and a spell hotbar (bottom centre) showing each spell's cooldown.
/// </summary>
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

    private Spell windingSpell;
    private float windEnd;

    /// <summary>True while a spell with a cast time is winding up.</summary>
    public bool IsCasting => windingSpell != null && Time.time < windEnd;
    /// <summary>Move speed multiplier for the controller (slowed while winding up).</summary>
    public float MoveMultiplier => IsCasting ? windingSpell.castMoveMultiplier : 1f;

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
        windingSpell = null;
        Vector3 origin = castPoint.position;
        spell.Cast(this, origin, AimDirection(origin));
    }

    private Vector3 AimDirection(Vector3 origin) => Aim.DirectionFrom(origin, aimCamera, transform, maxAimDistance);

    public float CooldownRemaining(Spell spell)
    {
        return readyTime.TryGetValue(spell, out float t) ? Mathf.Max(0f, t - Time.time) : 0f;
    }

    public void SetCastPoint(Transform point) => castPoint = point != null ? point : aimCamera;

    public void UnlockNext() => unlockedCount = Mathf.Min(unlockedCount + 1, spells.Count);

    private void OnGUI()
    {
        if (!showCrosshair || GameFlow.Paused) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        Spell spell = CurrentSpell;
        bool ready = spell == null || CooldownRemaining(spell) <= 0f;
        GUI.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);

        DrawHotbar();
    }

    // One slot per unlocked spell: number, name, a coloured strip, and a dark overlay that drains as the
    // cooldown runs out. The selected slot is raised and outlined.
    private void DrawHotbar()
    {
        int count = UnlockedCount;
        if (count == 0) return;
        float ui = Screen.height / 1080f;
        float w = 118f * ui, h = 54f * ui, gap = 8f * ui;
        float x0 = (Screen.width - (count * w + (count - 1) * gap)) * 0.5f;
        float y = Screen.height - h - 24f * ui;

        var nameStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, fontSize = Mathf.RoundToInt(15 * ui), alignment = TextAnchor.LowerCenter, fontStyle = FontStyle.Bold };
        var keyStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, fontSize = Mathf.RoundToInt(13 * ui), alignment = TextAnchor.UpperLeft };
        var cdStyle = new GUIStyle(GUI.skin.label) { font = MenuUI.Font, fontSize = Mathf.RoundToInt(20 * ui), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };

        for (int i = 0; i < count; i++)
        {
            Spell s = spells[i];
            bool sel = i == selected;
            var r = new Rect(x0 + i * (w + gap), y - (sel ? 8f * ui : 0f), w, h);

            if (sel) { GUI.color = s.uiColor; GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Texture2D.whiteTexture); }
            GUI.color = new Color(0f, 0f, 0f, sel ? 0.85f : 0.6f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = s.uiColor;
            GUI.DrawTexture(new Rect(r.x, r.yMax - 4f * ui, r.width, 4f * ui), Texture2D.whiteTexture);

            float cd = CooldownRemaining(s);
            if (cd > 0f && s.cooldown > 0f)
            {
                float frac = Mathf.Clamp01(cd / s.cooldown);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * frac), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            keyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(r.x + 5 * ui, r.y + 2 * ui, 30 * ui, 20 * ui), (i + 1).ToString(), keyStyle);
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
