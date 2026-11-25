using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game player HUD, built at runtime (uGUI):
///  - health bar (bottom left) with a trailing "damage" bar,
///  - parry meter under the crosshair (white = ready, cyan = window open, grey filling = cooldown),
///  - red screen flash when hurt, cyan flash + "PARRY!" pop on a successful parry.
/// Runs after the player controller so the parry cue uses this frame's camera.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerParry parry;

    private static readonly Color ReadyColor = new Color(1f, 1f, 1f, 0.9f);
    private static readonly Color WindowColor = new Color(0.35f, 0.9f, 1f, 1f);
    private static readonly Color CooldownColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
    private static readonly Color HealthColor = new Color(0.9f, 0.25f, 0.3f);
    private static readonly Color TrailColor = new Color(1f, 0.85f, 0.5f);

    private static readonly Color CueColor = new Color(0.85f, 0.3f, 1f, 1f);
    private const float CueRange = 40f;
    private Camera cam;

    private RectTransform healthFill, healthTrail, parryFill, cueRoot;
    private Image[] cueBrackets;
    private Vector2[] cueBracketHome;   // bracket positions at spread 1
    private Text cueText;
    private Image parryFillImage, flash;
    private Text healthText, parryText, popText;
    private float trailValue = 1f, flashAlpha;
    private int shownHealth = -1;
    private Color flashColor;
    private Coroutine popRoutine;

    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (parry == null) parry = GetComponent<PlayerParry>();
        Build();
    }

    private void OnEnable()
    {
        if (health != null) health.Damaged += OnDamaged;
        if (parry != null) { parry.Parried += OnParried; parry.Missed += OnMissed; }
    }

    private void OnDisable()
    {
        if (health != null) health.Damaged -= OnDamaged;
        if (parry != null) { parry.Parried -= OnParried; parry.Missed -= OnMissed; }
    }

    private void OnDamaged(DamageInfo info) => Flash(new Color(1f, 0.1f, 0.05f), 0.35f);
    private void OnParried(SpellProjectile p)
    {
        Flash(WindowColor, 0.25f);
        Pop("PARRY!", WindowColor);
    }
    private void OnMissed() => Pop("missed", CooldownColor, 0.6f);

    private void Update()
    {
        if (health != null)
        {
            float h = health.Normalized;
            healthFill.anchorMax = new Vector2(h, 1f);
            // The trail lags behind on damage, snaps up on heals.
            trailValue = h > trailValue ? h : Mathf.MoveTowards(trailValue, h, Time.unscaledDeltaTime * 0.6f);
            healthTrail.anchorMax = new Vector2(trailValue, 1f);
            int hp = Mathf.CeilToInt(health.Health);
            if (hp != shownHealth) { shownHealth = hp; healthText.text = hp.ToString(); }   // no new string every frame
        }

        if (parry != null)
        {
            float fill; Color c; string label;
            if (parry.WindowOpen) { fill = 1f; c = WindowColor; label = "PARRY"; }
            else if (parry.OnCooldown) { fill = 1f - parry.CooldownRemaining / parry.CooldownDuration; c = CooldownColor; label = parry.CooldownRemaining.ToString("0.0"); }
            else { fill = 1f; c = ReadyColor; label = "RMB"; }
            parryFill.anchorMax = new Vector2(fill, 1f);
            parryFillImage.color = c;
            parryText.text = label;
            parryText.color = c;
        }

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.8f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
    }

    // Purple [ ] brackets with "PARRY" on top lock onto the nearest enemy shot flying at you.
    // They close in as the shot arrives and turn cyan once it will reach the barrier within the
    // barrier's open time: press then. Cyan while the barrier is up, grey while parry is on cooldown.
    // LateUpdate (after the camera moved, see DefaultExecutionOrder) with the bolt's own position.
    private const float CueCloseTime = 0.6f;   // seconds before "press now" that the brackets start closing
    private void LateUpdate() => UpdateCue();

    private void UpdateCue()
    {
        SpellProjectile shot = parry != null ? IncomingShot() : null;
        if (cam == null) cam = Camera.main;
        Vector3 sp = shot != null && cam != null ? cam.WorldToScreenPoint(shot.transform.position) : Vector3.back;
        bool show = shot != null && sp.z > 0.1f;
        cueRoot.gameObject.SetActive(show);
        if (!show) return;

        cueRoot.position = new Vector3(sp.x, sp.y, 0f);
        float scale = Mathf.Clamp(8f / sp.z, 0.7f, 1.8f);
        cueRoot.localScale = Vector3.one * scale;

        // Time until the shot reaches the barrier sphere.
        float gap = Vector3.Distance(shot.transform.position, parry.BarrierCenter) - parry.BarrierRadius;
        float timeToImpact = Mathf.Max(0f, gap) / Mathf.Max(0.01f, shot.Speed);
        bool pressNow = timeToImpact <= parry.WindowDuration;
        float spread = Mathf.Lerp(0.6f, 1.6f, Mathf.Clamp01((timeToImpact - parry.WindowDuration) / CueCloseTime));
        for (int i = 0; i < cueBrackets.Length; i++)
            cueBrackets[i].rectTransform.anchoredPosition = cueBracketHome[i] * spread;

        Color c = parry.WindowOpen || (pressNow && parry.Ready) ? WindowColor : parry.OnCooldown ? CooldownColor : CueColor;
        foreach (var b in cueBrackets) b.color = c;
        cueText.color = c;
        cueText.text = "PARRY";
        cueText.rectTransform.localScale = Vector3.one / scale;        // constant text size
        cueText.rectTransform.anchoredPosition = new Vector2(0f, 32f + 16f / scale);
    }

    // Nearest parryable enemy shot within range that is heading at the player.
    private SpellProjectile IncomingShot()
    {
        Vector3 me = parry.BarrierCenter;
        SpellProjectile best = null;
        float bestD = CueRange * CueRange;
        foreach (var p in SpellProjectile.Active)
        {
            if (p == null || !p.Parryable || p.Faction != Faction.Enemy) continue;
            Vector3 to = me - p.transform.position;
            float d = to.sqrMagnitude;
            if (d >= bestD || Vector3.Dot(p.transform.forward, to.normalized) < 0.7f) continue;
            best = p;
            bestD = d;
        }
        return best;
    }

    private void Flash(Color color, float strength)
    {
        flashColor = color;
        flashAlpha = Mathf.Max(flashAlpha, strength);
    }

    private void Pop(string message, Color color, float scale = 1f)
    {
        if (popRoutine != null) StopCoroutine(popRoutine);
        popRoutine = StartCoroutine(PopRoutine(message, color, scale));
    }

    private IEnumerator PopRoutine(string message, Color color, float scale)
    {
        popText.text = message;
        var rt = popText.rectTransform;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f)
        {
            float s = scale * (t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f);
            rt.localScale = Vector3.one * s;
            popText.color = new Color(color.r, color.g, color.b, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
            yield return null;
        }
        popText.color = Color.clear;
    }

    // ------------------------------------------------------------------ construction
    private void Build()
    {
        var root = MenuUI.Canvas("PlayerHUD", 5).transform;
        root.SetParent(transform, false);

        // Full-screen flash (behind everything else).
        flash = Rect(root, "Flash", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear);

        // Health bar, bottom left (above the spell label).
        var hb = Rect(root, "HealthBar", Vector2.zero, Vector2.zero, new Vector2(40f, 80f), new Vector2(420f, 22f), new Color(0f, 0f, 0f, 0.55f));
        hb.rectTransform.pivot = Vector2.zero;
        healthTrail = Fill(hb.transform, "Trail", TrailColor);
        healthFill = Fill(hb.transform, "Fill", HealthColor);
        healthText = Label(root, "HealthText", 26, Color.white, TextAnchor.LowerLeft, new Vector2(470f, 76f), new Vector2(120f, 32f), Vector2.zero);

        // Parry meter, just under the crosshair.
        var pb = Rect(root, "ParryMeter", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -36f), new Vector2(80f, 6f), new Color(0f, 0f, 0f, 0.5f));
        parryFill = Fill(pb.transform, "Fill", ReadyColor);
        parryFillImage = parryFill.GetComponent<Image>();
        parryText = Label(root, "ParryText", 18, ReadyColor, TextAnchor.UpperCenter, new Vector2(0f, -42f), new Vector2(120f, 22f), new Vector2(0.5f, 0.5f));

        // "Parry now" cue: corner brackets around the crosshair.
        var cueGO = new GameObject("ParryCue", typeof(RectTransform));
        cueGO.transform.SetParent(root, false);
        cueRoot = (RectTransform)cueGO.transform;
        cueRoot.anchorMin = cueRoot.anchorMax = new Vector2(0.5f, 0.5f);
        cueRoot.sizeDelta = new Vector2(60f, 60f);
        cueBrackets = new Image[8];
        cueBracketHome = new Vector2[8];
        int k = 0;
        for (int sx = -1; sx <= 1; sx += 2)
        for (int sy = -1; sy <= 1; sy += 2)
        {
            // Each corner is an L made of a horizontal and a vertical bar.
            var h = Rect(cueRoot, "H", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx * 22f, sy * 30f), new Vector2(16f, 4f), CueColor);
            var v = Rect(cueRoot, "V", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(sx * 30f, sy * 22f), new Vector2(4f, 16f), CueColor);
            cueBracketHome[k] = h.rectTransform.anchoredPosition;
            cueBrackets[k++] = h;
            cueBracketHome[k] = v.rectTransform.anchoredPosition;
            cueBrackets[k++] = v;
        }
        cueText = Label(cueRoot, "CueText", 20, CueColor, TextAnchor.MiddleCenter, new Vector2(0f, 48f), new Vector2(160f, 26f), new Vector2(0.5f, 0.5f));
        cueText.text = "PARRY";
        cueText.fontStyle = FontStyle.Bold;
        cueGO.SetActive(false);

        // "PARRY!" pop, above the crosshair.
        popText = Label(root, "Pop", 44, Color.clear, TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(500f, 60f), new Vector2(0.5f, 0.5f));
        popText.fontStyle = FontStyle.Bold;
        popText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
    }

    private static Image Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color color)
    {
        var rt = MenuUI.Rect(parent, name, pos, size, color);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = rt.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    // A child that stretches over its parent; width is driven through anchorMax.x.
    private static RectTransform Fill(Transform parent, string name, Color color)
    {
        var img = Rect(parent, name, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, color);
        var rt = img.rectTransform;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static Text Label(Transform parent, string name, int size, Color color, TextAnchor align,
                              Vector2 pos, Vector2 box, Vector2 anchor)
    {
        var t = MenuUI.Label(parent, "", size, pos, box, color, align);
        t.name = name;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(anchor.x == 0f ? 0f : 0.5f, anchor.y == 0f ? 0f : 0.5f);
        rt.anchoredPosition = pos;
        return t;
    }
}
