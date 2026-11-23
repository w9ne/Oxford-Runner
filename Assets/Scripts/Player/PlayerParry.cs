using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Parry (no blocking). Right click opens a short parry window. Any parryable enemy projectile that
/// reaches the player, or comes within <see cref="catchRadius"/> of the camera, while the window is
/// open is reflected toward whatever is under the crosshair, faster and harder.
/// A window that catches nothing puts parry on cooldown; a successful parry has no cooldown.
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
public class PlayerParry : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private int mouseButton = 1;                // right click
    [SerializeField] private float window = 0.3f;
    [SerializeField] private float missCooldown = 2f;

    [Header("Catch")]
    [Tooltip("Projectiles this close to the camera during the window are parried before they hit.")]
    [SerializeField] private float catchRadius = 2.5f;

    [Header("Reflection")]
    [SerializeField] private Transform view;                     // the camera
    [SerializeField] private Transform reflectOrigin;            // where reflected shots leave (left hand cast point)
    [SerializeField] private float speedMultiplier = 2f;
    [SerializeField] private float damageMultiplier = 3f;
    [SerializeField] private Color reflectedTint = new Color(0.35f, 0.9f, 1f);

    [Header("Reward / Feel")]
    [SerializeField] private float healOnParry = 10f;
    [SerializeField] private float hitStopDuration = 0.07f;      // real-time seconds
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;

    /// <summary>Right click pressed and the window opened.</summary>
    public event Action WindowOpened;
    /// <summary>A projectile was reflected.</summary>
    public event Action<SpellProjectile> Parried;
    /// <summary>The window closed without catching anything (cooldown started).</summary>
    public event Action Missed;

    public bool WindowOpen => Time.time < windowEnd;
    public bool OnCooldown => Time.time < cooldownEnd;
    public bool Ready => !WindowOpen && !OnCooldown;
    public float CooldownRemaining => Mathf.Max(0f, cooldownEnd - Time.time);
    public float CooldownDuration => missCooldown;
    public float WindowDuration => window;

    /// <summary>
    /// True while an incoming parryable shot is close enough that pressing parry *now* will catch it.
    /// The HUD shows the "parry now" cue from this.
    /// </summary>
    public bool CueActive { get; private set; }
    /// <summary>0 when the cue first appears, 1 at the moment of impact.</summary>
    public float CueProgress { get; private set; }

    private float windowEnd = -1f, cooldownEnd = -1f;
    private bool windowPending;      // a window is (or was) open and hasn't been resolved yet
    private int caughtThisWindow;
    private PlayerHealth health;
    private bool hitStopping;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        if (view == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) view = cam.transform;
        }
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(mouseButton) && Cursor.lockState == CursorLockMode.Locked && Ready)
            OpenWindow();

        if (WindowOpen) ScanForIncoming();
        else if (windowPending) ResolveWindow();

        UpdateCue();
    }

    // A shot is caught once it's within catchRadius (or touching us) while the window is open.
    // So a press succeeds if it happens between "time to reach catchRadius + window" and impact.
    // The cue covers exactly that span, so pressing whenever it's visible always works.
    private void UpdateCue()
    {
        CueActive = false;
        if (view == null) return;

        Vector3 eye = view.position;
        float bestTti = float.MaxValue, bestLead = 1f;
        foreach (var p in SpellProjectile.Active)
        {
            if (p == null || !p.Parryable || p.Faction == Faction.Player || p.Speed <= 0f) continue;
            Vector3 toEye = eye - p.transform.position;
            float dist = toEye.magnitude;
            if (Vector3.Dot(p.transform.forward, toEye / Mathf.Max(dist, 0.001f)) < 0.8f) continue;   // not coming at us
            float tti = dist / p.Speed;
            float lead = window + catchRadius / p.Speed;
            if (tti <= lead && tti < bestTti) { bestTti = tti; bestLead = lead; }
        }

        if (bestTti < float.MaxValue)
        {
            CueActive = true;
            CueProgress = 1f - Mathf.Clamp01(bestTti / bestLead);
        }
    }

    private void OpenWindow()
    {
        windowEnd = Time.time + window;
        windowPending = true;
        caughtThisWindow = 0;
        WindowOpened?.Invoke();
        ScanForIncoming();
    }

    private void ResolveWindow()
    {
        windowPending = false;
        if (caughtThisWindow > 0) return;
        cooldownEnd = Time.time + missCooldown;
        Missed?.Invoke();
    }

    // Proactively catch shots that are about to hit, so a parry doesn't depend on exact collision timing.
    private void ScanForIncoming()
    {
        if (view == null) return;
        Vector3 eye = view.position;
        float r2 = catchRadius * catchRadius;
        SpellProjectile best = null;
        float bestD = float.MaxValue;
        foreach (var p in SpellProjectile.Active)
        {
            if (p == null || !p.Parryable || p.Faction == Faction.Player) continue;
            float d = (p.transform.position - eye).sqrMagnitude;
            if (d < r2 && d < bestD) { best = p; bestD = d; }
        }
        // Reflect outside the enumeration (Reflect doesn't change the set, but keep it tidy).
        if (best != null) Reflect(best);
    }

    /// <summary>Called by a projectile that's touching the player. Returns true if it got parried.</summary>
    public bool TryCatch(SpellProjectile projectile)
    {
        if (!WindowOpen || projectile == null || !projectile.Parryable) return false;
        Reflect(projectile);
        return true;
    }

    private void Reflect(SpellProjectile p)
    {
        Vector3 origin = reflectOrigin != null ? reflectOrigin.position : view.position + view.forward * 0.8f;
        Vector3 dir = Aim.DirectionFrom(origin, view, transform);
        p.Reflect(origin, dir, transform, Faction.Player, speedMultiplier, damageMultiplier, reflectedTint);

        caughtThisWindow++;
        if (health != null) health.Heal(healOnParry);
        Parried?.Invoke(p);
        if (!hitStopping) StartCoroutine(HitStop());
    }

    // A tiny freeze-frame sells the impact.
    private IEnumerator HitStop()
    {
        hitStopping = true;
        float previous = Time.timeScale;
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(hitStopDuration);
        Time.timeScale = previous <= hitStopTimeScale ? 1f : previous;
        hitStopping = false;
    }
}
