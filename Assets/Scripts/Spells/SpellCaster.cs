using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lets the player cast spells. Hold Mouse 1 (left click) to cast the selected spell whenever it's off cooldown.
/// Aims at whatever is under the crosshair, firing from a hand point so shots line up with the screen center.
/// Number keys 1-9 select among unlocked spells (spells unlock per stage later via UnlockNext()).
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

        if (Cursor.lockState != CursorLockMode.Locked) return;   // first click just re-locks the cursor
        if (Input.GetMouseButton(0)) TryCast();
    }

    public bool TryCast()
    {
        Spell spell = CurrentSpell;
        if (spell == null || CooldownRemaining(spell) > 0f) return false;

        Vector3 origin = castPoint.position;
        Vector3 dir = AimDirection(origin);
        spell.Cast(this, origin, dir);
        readyTime[spell] = Time.time + spell.cooldown;
        Cast?.Invoke(spell);
        return true;
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
        if (!showCrosshair) return;
        float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
        Spell spell = CurrentSpell;
        bool ready = spell == null || CooldownRemaining(spell) <= 0f;
        GUI.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        GUI.DrawTexture(new Rect(cx - 2, cy - 2, 4, 4), Texture2D.whiteTexture);

        if (spell != null)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.LowerLeft };
            style.normal.textColor = spell.uiColor;
            GUI.color = Color.white;
            float cd = CooldownRemaining(spell);
            GUI.Label(new Rect(16, Screen.height - 46, 400, 30),
                      $"[{selected + 1}] {spell.displayName}" + (cd > 0f ? $"  {cd:0.0}s" : ""), style);
        }
    }
}
