using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// In-game player HUD, built at runtime (uGUI):
///  - big health bar (bottom left): colour runs green -> yellow -> red, a trailing "damage" bar, 25 HP ticks,
///    large numbers, pulses when low,
///  - two square ability boxes right of it, filling bottom to top as they recharge: dash (chevrons) and parry
///    (shield; flashes green while the barrier is up, refills after a missed parry),
///  - red screen flash when hurt, green flash + "PARRY!" pop on a successful parry.
/// The spell hotbar (SpellCaster) sits bottom right so they never overlap.
/// Runs after the player controller so the parry cue uses this frame's camera.
/// </summary>
[DefaultExecutionOrder(100)]
public class PlayerHUD : MonoBehaviour
{
    [SerializeField] private PlayerHealth health;
    [SerializeField] private PlayerParry parry;

    private static readonly Color WindowColor = new Color(0.3f, 1f, 0.4f, 1f);   // parry green: press now / barrier up / parried
    private static readonly Color CooldownColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
    private static readonly Color HealthHigh = new Color(0.3f, 0.95f, 0.45f), HealthMid = new Color(1f, 0.85f, 0.25f), HealthLow = new Color(1f, 0.2f, 0.2f);
    private static readonly Color TrailColor = new Color(1f, 1f, 1f, 0.85f);
    private static readonly Color DashReady = new Color(0.45f, 0.85f, 1f), DashCharging = new Color(0.5f, 0.55f, 0.65f);
    private const float BarWidth = 520f, BarHeight = 30f, BoxSize = 64f;
    /// <summary>Width (canvas units at 1920x1080) taken by the health bar + ability boxes, bottom left; the hotbar keeps clear of it.</summary>
    public const float LeftWidth = 36f + BarWidth + 8f + 20f + BoxSize * 2f + 12f + 24f;

    private static readonly Color CueColor = new Color(0.85f, 0.3f, 1f, 1f);
    private const float CueRange = 40f;
    private Camera cam;

    private RectTransform healthFill, healthTrail, cueRoot;
    private Image[] cueBrackets;
    private Vector2[] cueBracketHome;   // bracket positions at spread 1
    private Text cueText;
    private Image healthFillImage, dashFill, dashIcon, parryFill, parryIcon, flash;
    private Text healthText, maxText, popText;
    private FirstPersonController controller;
    private float trailValue = 1f, flashAlpha;
    private int shownHealth = -1;
    private Color flashColor;
    private Coroutine popRoutine;

    private void Awake()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (parry == null) parry = GetComponent<PlayerParry>();
        controller = GetComponent<FirstPersonController>();
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
    private void OnParried(SpellProjectile p)   // green, like the press-now cue
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
            Color c = h > 0.5f ? Color.Lerp(HealthMid, HealthHigh, (h - 0.5f) * 2f) : Color.Lerp(HealthLow, HealthMid, h * 2f);
            if (h < 0.3f) c = Color.Lerp(c, Color.white, 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f)));   // low: pulse
            healthFillImage.color = c;
            // The trail lags behind on damage, snaps up on heals.
            trailValue = h > trailValue ? h : Mathf.MoveTowards(trailValue, h, Time.unscaledDeltaTime * 0.6f);
            healthTrail.anchorMax = new Vector2(trailValue, 1f);
            int hp = Mathf.CeilToInt(health.Health);
            if (hp != shownHealth) { shownHealth = hp; healthText.text = hp.ToString(); }   // no new string every frame
            healthText.color = h < 0.3f ? c : Color.white;
        }

        if (controller != null)
        {
            float d = controller.DashCharge;
            dashFill.fillAmount = d;
            dashFill.color = d >= 1f ? new Color(DashReady.r, DashReady.g, DashReady.b, 0.55f) : DashCharging;
            dashIcon.color = d >= 1f ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        if (parry != null)
        {
            bool cooling = parry.OnCooldown;
            parryFill.fillAmount = cooling ? 1f - parry.CooldownRemaining / Mathf.Max(0.01f, parry.CooldownDuration) : 1f;
            parryFill.color = parry.WindowOpen ? WindowColor : cooling ? DashCharging : new Color(WindowColor.r, WindowColor.g, WindowColor.b, 0.45f);
            parryIcon.color = cooling ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.8f);
        flash.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashAlpha);
    }

    // Purple [ ] brackets with "PARRY" on top lock onto the nearest enemy shot flying at you.
    // They close in as the shot arrives and turn green with a big "!" once it will reach the barrier within the
    // barrier's open time: press then. Green while the barrier is up, grey while parry is on cooldown.
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

        bool go = parry.WindowOpen || (pressNow && parry.Ready);
        Color c = go ? WindowColor : parry.OnCooldown ? CooldownColor : CueColor;
        foreach (var b in cueBrackets) b.color = c;
        cueText.color = c;
        cueText.text = go ? "!" : "PARRY";
        cueText.rectTransform.localScale = Vector3.one * ((go ? 2.2f : 1f) / scale);   // constant size; "!" big
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

        // Health: big bar bottom left, number above it.
        var frame = Rect(root, "HealthFrame", Vector2.zero, Vector2.zero, new Vector2(36f, 36f), new Vector2(BarWidth + 8f, BarHeight + 8f), new Color(0f, 0f, 0f, 0.75f));
        frame.rectTransform.pivot = Vector2.zero;
        var hb = Rect(frame.transform, "HealthBar", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.15f, 0.02f, 0.02f, 0.9f));
        hb.rectTransform.offsetMin = new Vector2(4f, 4f); hb.rectTransform.offsetMax = new Vector2(-4f, -4f);
        healthTrail = Fill(hb.transform, "Trail", TrailColor);
        healthFill = Fill(hb.transform, "Fill", HealthHigh);
        healthFillImage = healthFill.GetComponent<Image>();
        for (int i = 1; i < 4; i++)   // a tick every 25 HP
        {
            var tick = Rect(hb.transform, "Tick", new Vector2(i * 0.25f, 0f), new Vector2(i * 0.25f, 1f), Vector2.zero, new Vector2(3f, 0f), new Color(0f, 0f, 0f, 0.55f));
            tick.rectTransform.offsetMin = new Vector2(-1.5f, 0f); tick.rectTransform.offsetMax = new Vector2(1.5f, 0f);
        }
        healthText = Label(root, "HealthText", 64, Color.white, TextAnchor.LowerLeft, new Vector2(40f, 76f), new Vector2(170f, 70f), Vector2.zero);
        healthText.fontStyle = FontStyle.Bold;
        healthText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3f, -3f);
        maxText = Label(root, "HealthMax", 28, new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerLeft, new Vector2(195f, 84f), new Vector2(120f, 36f), Vector2.zero);
        maxText.text = "/ " + Mathf.RoundToInt(health != null ? health.MaxHealth : 100f);
        maxText.gameObject.AddComponent<Outline>().effectDistance = new Vector2(2f, -2f);

        // Ability boxes right of the health bar: dash, then parry. Each fills bottom to top as it recharges.
        float boxX = 36f + BarWidth + 8f + 20f;
        AbilityBox(root, "DashBox", new Vector2(boxX, 36f), HudIcons.Dash, out dashFill, out dashIcon);
        AbilityBox(root, "ParryBox", new Vector2(boxX + BoxSize + 12f, 36f), HudIcons.Shield, out parryFill, out parryIcon);

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

    // A square box: dark frame, a fill that rises from the bottom (Image.fillAmount), the icon on top.
    private static void AbilityBox(Transform root, string name, Vector2 pos, Sprite icon, out Image fill, out Image iconImage)
    {
        var frame = Rect(root, name, Vector2.zero, Vector2.zero, pos, new Vector2(BoxSize, BoxSize), new Color(0f, 0f, 0f, 0.75f));
        frame.rectTransform.pivot = Vector2.zero;
        fill = Rect(frame.transform, "Fill", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        fill.rectTransform.offsetMin = new Vector2(4f, 4f); fill.rectTransform.offsetMax = new Vector2(-4f, -4f);
        fill.sprite = HudIcons.White;
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Vertical;
        fill.fillOrigin = (int)Image.OriginVertical.Bottom;
        iconImage = Rect(frame.transform, "Icon", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white);
        iconImage.rectTransform.offsetMin = new Vector2(8f, 8f); iconImage.rectTransform.offsetMax = new Vector2(-8f, -8f);
        iconImage.sprite = icon;
        iconImage.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
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
