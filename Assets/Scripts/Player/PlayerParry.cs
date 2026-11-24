using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Parry barrier. Right click raises a magic barrier for <see cref="window"/> seconds (0.3).
/// Any parryable enemy projectile that actually HITS the player (collides with the player's capsule)
/// while it is up is reflected toward the crosshair, 2x faster and harder. Nothing is caught early or by
/// proximity. Splash damage is also blocked while the barrier is up.
/// A barrier that catches nothing puts parry on cooldown; a successful parry has no cooldown.
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
public class PlayerParry : MonoBehaviour
{
    /// <summary>The player's parry (one player per scene). Projectiles test against its barrier.</summary>
    public static PlayerParry Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private int mouseButton = 1;                // right click
    [SerializeField] private float window = 0.3f;
    [SerializeField] private float missCooldown = 2f;

    [Header("Effects (Hovl, set by Oxford Runner/Setup Spell VFX)")]
    [SerializeField] private GameObject barrierVfx;      // shield shown while the barrier is up
    [SerializeField] private GameObject parryVfx;        // burst where a bolt got parried

    [Header("Barrier (enemy bolts that enter this sphere while it is up get reflected)")]
    [SerializeField] private float barrierRadius = 1.1f;
    [Tooltip("Barrier centre, measured down from the camera.")]
    [SerializeField] private float chestDrop = 0.35f;
    [Tooltip("Where the shield effect sits, in camera space (its pivot is at the shield's base).")]
    [SerializeField] private Vector3 barrierVfxOffset = new Vector3(0f, -0.6f, 0.9f);
    [SerializeField] private Color barrierColor = new Color(0.35f, 0.9f, 1f, 0.35f);

    [Header("Reflection")]
    [SerializeField] private Transform view;                     // the camera
    [SerializeField] private float speedMultiplier = 2f;
    [SerializeField] private float damageMultiplier = 3f;
    [SerializeField] private Color reflectedTint = new Color(0.35f, 0.9f, 1f);

    [Header("Reward / Feel")]
    [SerializeField] private float healOnParry = 10f;
    [SerializeField] private float hitStopDuration = 0.07f;      // real-time seconds
    [SerializeField, Range(0f, 1f)] private float hitStopTimeScale = 0.05f;

    /// <summary>Right click pressed and the barrier went up.</summary>
    public event Action WindowOpened;
    /// <summary>A projectile was reflected.</summary>
    public event Action<SpellProjectile> Parried;
    /// <summary>The barrier dropped without catching anything (cooldown started).</summary>
    public event Action Missed;

    public bool WindowOpen => Time.time < windowEnd;
    public bool OnCooldown => Time.time < cooldownEnd;
    public bool Ready => !WindowOpen && !OnCooldown;
    public float CooldownRemaining => Mathf.Max(0f, cooldownEnd - Time.time);
    public float CooldownDuration => missCooldown;
    public float WindowDuration => window;
    public float BarrierRadius => barrierRadius;
    public Vector3 BarrierCenter => view != null ? view.position + Vector3.down * chestDrop : transform.position;

    private float windowEnd = -1f, cooldownEnd = -1f, openedAt = -1f, flashUntil = -1f;
    private bool windowPending;      // a barrier is (or was) up and hasn't been resolved yet
    private int caughtThisWindow;
    private PlayerHealth health;
    private bool hitStopping;
    private Transform barrierVisual;
    private Material barrierMaterial;

    private void Awake()
    {
        Instance = this;
        health = GetComponent<PlayerHealth>();
        if (view == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) view = cam.transform;
        }
        BuildBarrierVisual();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(mouseButton) && Cursor.lockState == CursorLockMode.Locked && Ready)
            OpenWindow();

        if (!WindowOpen && windowPending) ResolveWindow();
        UpdateBarrierVisual();
    }

    private void OpenWindow()
    {
        windowEnd = Time.time + window;
        openedAt = Time.time;
        windowPending = true;
        caughtThisWindow = 0;
        if (barrierVfx != null && view != null)
        {
            var fx = Instantiate(barrierVfx, view);
            fx.transform.localPosition = barrierVfxOffset;
            fx.transform.localRotation = Quaternion.identity;
            Destroy(fx, window + 0.35f);
        }
        WindowOpened?.Invoke();
    }

    private void ResolveWindow()
    {
        windowPending = false;
        if (caughtThisWindow > 0) return;
        cooldownEnd = Time.time + missCooldown;
        Missed?.Invoke();
    }

    /// <summary>Called by a projectile that collided with the player. Returns true if it got parried.</summary>
    public bool TryCatch(SpellProjectile projectile, Vector3 contactPoint)
    {
        if (!WindowOpen || projectile == null || !projectile.Parryable) return false;
        Vector3 dir = Aim.DirectionFrom(contactPoint, view, transform);
        projectile.Reflect(contactPoint, dir, transform, Faction.Player, speedMultiplier, damageMultiplier, reflectedTint);

        caughtThisWindow++;
        if (parryVfx != null) Destroy(Instantiate(parryVfx, contactPoint, Quaternion.identity), 2f);
        flashUntil = Time.unscaledTime + 0.15f;
        if (health != null) health.Heal(healOnParry);
        Parried?.Invoke(projectile);
        if (!hitStopping) StartCoroutine(HitStop());
        return true;
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

    // ---------- Barrier visual ----------
    // A translucent shell in front of the camera that pops up when the barrier is raised and fades
    // over the window, flashing white on a successful parry.
    private void BuildBarrierVisual()
    {
        if (view == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "ParryBarrier";
        DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(view, false);
        var mr = go.GetComponent<MeshRenderer>();
        barrierMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ParryBarrier" };
        mr.sharedMaterial = barrierMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        barrierVisual = go.transform;
        go.SetActive(false);
    }

    private void UpdateBarrierVisual()
    {
        if (barrierVisual == null) return;
        bool flashing = Time.unscaledTime < flashUntil;
        bool show = WindowOpen || flashing;
        if (barrierVisual.gameObject.activeSelf != show) barrierVisual.gameObject.SetActive(show);
        if (!show) return;

        float t = Mathf.Clamp01((Time.time - openedAt) / window);
        float pop = t < 0.2f ? Mathf.Lerp(0.6f, 1.05f, t / 0.2f) : Mathf.Lerp(1.05f, 1f, (t - 0.2f) / 0.8f);
        // A flattened shell on the front of the barrier sphere, just under the eye line.
        barrierVisual.localPosition = new Vector3(0f, -chestDrop * 0.5f, barrierRadius * 0.8f);
        barrierVisual.localScale = new Vector3(1.7f, 1.3f, 0.25f) * pop;
        float alpha = flashing ? 0.7f : Mathf.Lerp(barrierColor.a, barrierColor.a * 0.4f, t);
        barrierMaterial.color = flashing ? new Color(1f, 1f, 1f, alpha) : new Color(barrierColor.r, barrierColor.g, barrierColor.b, alpha);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.5f);
        Gizmos.DrawWireSphere(BarrierCenter, barrierRadius);
    }
}
