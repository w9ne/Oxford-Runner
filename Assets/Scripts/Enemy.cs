using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>What an enemy is. Each kind is its own prefab (see EnemySetup) with tuned fields.</summary>
public enum EnemyKind { Caster, Skeleton, Mage, Commander }

/// <summary>
/// Enemy AI, Rigidbody-driven. One script, several kinds (set up per prefab):
///  - Caster    : the original: walks in to a stopping distance and fires telegraphed bolts.
///  - Skeleton  : melee. Charges you with a sword; each swing winds up (glow + raised sword) before it lands.
///  - Mage      : squishy ranged caster. Keeps a band of distance, strafes, fires weak bolts.
///  - Commander : tanky ranged caster. Stays further out, fires faster, moves faster and dashes sideways
///                to dodge your spells (every few seconds).
/// Everyone steers around walls: they probe ahead, pick a clear direction to slide around obstacles and
/// ledges, and change side when they get stuck. Ranged kinds reposition when a wall blocks their shot.
/// Also: Frost freeze, slow, knockback stagger, hit flash, health bar, fall death.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class Enemy : MonoBehaviour, IDamageable, IHealthReadout
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    /// <summary>All living, enabled enemies (used by the missile lock-on).</summary>
    public static readonly List<Enemy> All = new List<Enemy>();

    [Header("Type")]
    [SerializeField] private EnemyKind kind = EnemyKind.Caster;
    [Tooltip("Multipliers applied to the wave's health / speed / fire interval when spawned.")]
    [SerializeField] private float healthScale = 1f;
    [SerializeField] private float speedScale = 1f;
    [SerializeField] private float intervalScale = 1f;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 50f;     // common enemy base HP
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 8f;   // ranged casters keep some distance
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("If true, only parried (reflected) projectiles can hurt this enemy.")]
    [SerializeField] private bool onlyReflectedDamage;

    [Header("Ranged spacing (keepMax 0 = just stop at stoppingDistance)")]
    [SerializeField] private float keepMin = 0f;
    [SerializeField] private float keepMax = 0f;
    [SerializeField] private float strafeSpeedFactor = 0.6f;

    [Header("Attack")]
    [SerializeField] private ProjectileSpell attackSpell;
    [SerializeField] private float fireInterval = 3f;
    [SerializeField] private float windupTime = 0.6f;       // telegraph before each shot
    [SerializeField] private float attackRange = 35f;
    [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1.1f, 0.7f);
    [SerializeField] private Renderer telegraphRenderer;    // glows during wind-up (the "Face")
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.9f);

    [Header("Melee (Skeleton)")]
    [SerializeField] private bool melee;
    [SerializeField] private float meleeRange = 2.2f;
    [SerializeField] private float meleeWindup = 0.6f;      // at least 0.5 s to see it coming
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeCooldown = 1.2f;
    [SerializeField] private Transform weapon;              // swings during the attack

    [Header("Dash (Commander)")]
    [SerializeField] private bool canDash;
    [SerializeField] private float dashCooldown = 5f;
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.25f;

    public event Action<Enemy> Died;

    public EnemyKind Kind => kind;
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float FireInterval => fireInterval;
    public bool IsDead { get; private set; }
    public Faction Faction => Faction.Enemy;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform target;
    private Camera targetView;
    private IDamageable targetHealth;
    private float nextShotTime, windupStart = -1f;
    private MaterialPropertyBlock block;
    private Color telegraphBase = Color.white;
    private float hitFlash;
    private Renderer[] renderers;
    private Color[] baseColors;
    private float slowFactor = 1f, slowUntil, staggerUntil;
    private static readonly Color SlowTint = new Color(0.55f, 0.85f, 1f);
    private static readonly Color FrozenTint = new Color(0.75f, 0.92f, 1f);

    // Steering
    private int avoidSide = 1;
    private float stuckTime, detourUntil;
    private Vector3 detourDir;
    private float strafeSign = 1f, nextStrafeFlip;
    private bool hasLineOfSight = true;
    private float noSightTime, nextSightCheck;

    // Dash / melee animation
    private float nextDash, dashUntil;
    private Vector3 dashVelocity;
    private float swingTime = -1f;
    private Quaternion weaponRest;

    [Header("Frozen")]
    [SerializeField] private float frozenDamageMultiplier = 2.5f;
    private float frozenUntil;
    private bool frozenApplied;
    private RigidbodyConstraints savedConstraints;
    private GUIStyle frozenStyle;

    /// <summary>Frozen solid (Frost Lance): can't move, turn or cast, and takes extra damage.</summary>
    public bool IsFrozen => Time.time < frozenUntil;
    /// <summary>Stunned (Lightning): stopped in place, no attacks.</summary>
    public bool IsStunned => Time.time < stunUntil;
    private float stunUntil;

    /// <summary>1 = normal speed, lower while slowed (Frost Lance).</summary>
    private float SpeedScale => Time.time < slowUntil ? slowFactor : 1f;
    private float CurrentWindup => melee ? meleeWindup : windupTime;
    private bool Winding => windupStart >= 0f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        Health = maxHealth;
        block = new MaterialPropertyBlock();
        renderers = GetComponentsInChildren<Renderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;
        }
        if (telegraphRenderer == null)
        {
            var face = transform.Find("Face");
            if (face != null) telegraphRenderer = face.GetComponent<Renderer>();
        }
        if (telegraphRenderer != null && telegraphRenderer.sharedMaterial != null && telegraphRenderer.sharedMaterial.HasProperty(ColorId))
            telegraphBase = telegraphRenderer.sharedMaterial.GetColor(ColorId);
        if (weapon != null) weaponRest = weapon.localRotation;
        avoidSide = UnityEngine.Random.value < 0.5f ? -1 : 1;
        strafeSign = avoidSide;
    }

    private void OnEnable() => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private void Start()
    {
        if (GetComponent<WorldHealthBar>() == null) gameObject.AddComponent<WorldHealthBar>();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            targetView = player.GetComponentInChildren<Camera>();
            targetHealth = player.GetComponent<IDamageable>();
        }
        // Stagger the first attack so a group doesn't fire in unison.
        nextShotTime = Time.time + fireInterval * UnityEngine.Random.Range(0.5f, 1.2f);
        nextDash = Time.time + UnityEngine.Random.Range(1f, dashCooldown);
    }

    /// <summary>Set by the spawner. The prefab's kind multipliers are applied on top.</summary>
    public void Configure(float health, float speed, float interval)
    {
        maxHealth = health * healthScale;
        Health = maxHealth;
        moveSpeed = speed * speedScale;
        fireInterval = interval * intervalScale;
    }

    /// <summary>World bounds of the enemy's own model (not its health bar), for the lock-on brackets.</summary>
    public Bounds VisualBounds
    {
        get
        {
            Bounds b = new Bounds(transform.position, Vector3.one * 0.5f);
            bool any = false;
            foreach (var r in renderers)
            {
                if (r == null || r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return b;
        }
    }

    // ================================================================== movement
    private void FixedUpdate()
    {
        if (IsDead || target == null) return;
        if (transform.position.y < FallDeathHeight) { Die(); return; }   // knocked into a pit
        if (IsFrozen) return;
        if (IsStunned)
        {
            if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;

        if (!rb.isKinematic && Time.time >= staggerUntil)   // stationary turrets are kinematic and only rotate; knockback staggers
        {
            Vector3 v = rb.linearVelocity;
            Vector3 horizontal;
            if (Time.time < dashUntil) horizontal = dashVelocity;
            else
            {
                Vector3 wish = DesiredMove(dir, dist, out float speedFactor);
                Vector3 steer = Steer(wish);
                horizontal = steer * (moveSpeed * speedFactor * SpeedScale);
                TrackStuck(steer, v);
            }
            rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
        }

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, turnSpeed * SpeedScale * Time.fixedDeltaTime));
    }

    // Where this kind wants to go (unit vector or zero), before wall avoidance.
    private Vector3 DesiredMove(Vector3 dir, float dist, out float speedFactor)
    {
        speedFactor = 1f;
        if (Winding) return Vector3.zero;            // plant feet to swing / cast

        if (melee) return dist > meleeRange * 0.85f ? dir : Vector3.zero;

        // Ranged: no clear shot for a while -> move to find one (walk in and to the side).
        if (!hasLineOfSight && noSightTime > 1f)
            return (dir + Vector3.Cross(Vector3.up, dir) * strafeSign * 0.7f).normalized;

        if (keepMax <= 0f) return dist > stoppingDistance ? dir : Vector3.zero;   // classic caster

        if (dist < keepMin) return -dir;             // too close: back off
        if (dist > keepMax) return dir;              // too far: close in
        if (Time.time >= nextStrafeFlip)
        {
            strafeSign = -strafeSign;
            nextStrafeFlip = Time.time + UnityEngine.Random.Range(1.5f, 3.5f);
        }
        speedFactor = strafeSpeedFactor;
        return Vector3.Cross(Vector3.up, dir) * strafeSign;   // circle at range
    }

    // Wall recognition: if the way ahead is blocked (wall, obstacle or a drop), slide around it.
    private Vector3 Steer(Vector3 wish)
    {
        if (wish.sqrMagnitude < 0.01f) return Vector3.zero;
        if (Time.time < detourUntil && PathClear(detourDir)) return detourDir;
        if (PathClear(wish)) return wish;

        for (int step = 1; step <= 5; step++)
        {
            float angle = step * 30f;
            for (int s = 0; s < 2; s++)
            {
                int side = s == 0 ? avoidSide : -avoidSide;
                Vector3 d = Quaternion.Euler(0f, angle * side, 0f) * wish;
                if (!PathClear(d)) continue;
                avoidSide = side;
                detourDir = d;
                detourUntil = Time.time + 0.5f;
                return d;
            }
        }
        avoidSide = -avoidSide;
        if (keepMax > 0f) strafeSign = -strafeSign;
        return Vector3.zero;
    }

    private static readonly RaycastHit[] probeHits = new RaycastHit[8];

    private bool PathClear(Vector3 d)
    {
        if (!GroundAhead(d)) return false;
        float radius = capsule.radius * Mathf.Abs(transform.lossyScale.x) * 0.9f;
        Vector3 from = transform.position + Vector3.up * 0.2f;
        int n = Physics.SphereCastNonAlloc(from, radius, d, probeHits, 1.4f, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var h = probeHits[i];
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (target != null && h.collider.transform.IsChildOf(target)) continue;
            if (h.collider.GetComponentInParent<Enemy>() != null) continue;   // crowds push through each other
            if (h.normal.y > 0.6f) continue;                                    // floor / gentle ramp
            if (h.distance <= 0f) continue;                                     // already touching (start overlap)
            return false;
        }
        return true;
    }

    // Wanted to move but barely moving -> wedged on something: try the other side for a bit.
    private void TrackStuck(Vector3 steer, Vector3 velocity)
    {
        if (steer.sqrMagnitude < 0.01f) { stuckTime = 0f; return; }
        float actual = new Vector3(velocity.x, 0f, velocity.z).magnitude;
        stuckTime = actual < moveSpeed * 0.2f ? stuckTime + Time.fixedDeltaTime : 0f;
        if (stuckTime > 0.6f)
        {
            stuckTime = 0f;
            avoidSide = -avoidSide;
            detourDir = Quaternion.Euler(0f, 100f * avoidSide, 0f) * steer;
            detourUntil = Time.time + 0.8f;
        }
    }

    private const float FallDeathHeight = -15f;

    // Is there floor a step ahead? Keeps walkers from strolling off platforms into pits.
    private bool GroundAhead(Vector3 dir)
    {
        Vector3 probe = transform.position + dir * 1.2f + Vector3.up * 0.5f;
        return Physics.Raycast(probe, Vector3.down, 3.5f, ~0, QueryTriggerInteraction.Ignore);
    }

    // ================================================================== per frame
    private void Update()
    {
        if (IsDead) return;
        UpdateFreeze();
        if (!IsFrozen && !IsStunned)
        {
            UpdateSight();
            if (melee) UpdateMelee(); else UpdateAttack();
            if (canDash) UpdateDash();
        }
        UpdateWeapon();
        UpdateColors();
    }

    private void UpdateSight()
    {
        if (target == null || Time.time < nextSightCheck) { if (!hasLineOfSight) noSightTime += Time.deltaTime; return; }
        nextSightCheck = Time.time + 0.2f;
        hasLineOfSight = CanSeeTarget(ignoreRange: true);
        if (hasLineOfSight) noSightTime = 0f; else noSightTime += Time.deltaTime;
    }

    // ---------- Ranged attack ----------
    private void UpdateAttack()
    {
        if (attackSpell == null || target == null) return;

        if (windupStart < 0f)
        {
            if (Time.time >= nextShotTime && CanSeeTarget()) windupStart = Time.time;
            return;
        }

        if (Time.time - windupStart >= windupTime / SpeedScale)
        {
            windupStart = -1f;
            nextShotTime = Time.time + fireInterval;
            if (CanSeeTarget()) Shoot();
        }
    }

    // ---------- Melee attack ----------
    private void UpdateMelee()
    {
        if (target == null) return;
        Vector3 to = target.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        if (windupStart < 0f)
        {
            if (Time.time >= nextShotTime && dist <= meleeRange && Mathf.Abs(target.position.y - transform.position.y) < 2f)
                windupStart = Time.time;
            return;
        }

        if (Time.time - windupStart >= meleeWindup / SpeedScale)
        {
            windupStart = -1f;
            nextShotTime = Time.time + meleeCooldown;
            swingTime = Time.time;
            // Lands if you're still in reach and roughly in front.
            bool inReach = dist <= meleeRange + 0.6f && Vector3.Angle(transform.forward, to) < 75f
                           && Mathf.Abs(target.position.y - transform.position.y) < 2f;
            if (inReach && targetHealth != null && !targetHealth.IsDead)
                targetHealth.TakeDamage(new DamageInfo(meleeDamage, Faction.Enemy, target.position + Vector3.up));
        }
    }

    // Sword: raised back during the wind-up, quick chop on the swing, then back to rest.
    private void UpdateWeapon()
    {
        if (weapon == null) return;
        Quaternion pose = weaponRest;
        if (Winding && melee)
        {
            float k = Mathf.Clamp01((Time.time - windupStart) / meleeWindup);
            pose = weaponRest * Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, k), 0f, 0f);
        }
        else if (swingTime >= 0f && Time.time - swingTime < 0.35f)
        {
            float k = (Time.time - swingTime) / 0.35f;
            float a = k < 0.3f ? Mathf.Lerp(-110f, 70f, k / 0.3f) : Mathf.Lerp(70f, 0f, (k - 0.3f) / 0.7f);
            pose = weaponRest * Quaternion.Euler(a, 0f, 0f);
        }
        weapon.localRotation = Quaternion.Slerp(weapon.localRotation, pose, 1f - Mathf.Exp(-30f * Time.deltaTime));
    }

    // ---------- Dash (dodges your spells) ----------
    private void UpdateDash()
    {
        if (Time.time < nextDash || rb.isKinematic || Time.time < staggerUntil) return;
        foreach (var p in SpellProjectile.Active)
        {
            if (p == null || p.Faction != Faction.Player) continue;
            Vector3 toMe = transform.position - p.transform.position;
            if (toMe.sqrMagnitude > 14f * 14f) continue;
            if (Vector3.Dot(p.transform.forward, toMe.normalized) < 0.9f) continue;   // not coming at me
            Vector3 side = Vector3.Cross(Vector3.up, p.transform.forward).normalized;
            if (Vector3.Dot(side, toMe) < 0f) side = -side;     // dodge to the side it's already off
            if (!PathClear(side)) side = -side;
            if (!PathClear(side)) return;
            StartDash(side);
            return;
        }
        // Too close to the player: dash back out to range.
        if (target != null && keepMin > 0f)
        {
            Vector3 away = transform.position - target.position; away.y = 0f;
            if (away.magnitude < keepMin * 0.5f && PathClear(away.normalized)) StartDash(away.normalized);
        }
    }

    private void StartDash(Vector3 dir)
    {
        dashVelocity = dir * dashSpeed;
        dashUntil = Time.time + dashDuration;
        nextDash = Time.time + dashCooldown;
        windupStart = -1f;
        hitFlash = 0.5f;
    }

    private Vector3 Muzzle => transform.TransformPoint(muzzleOffset);

    private Vector3 AimPoint => targetView != null
        ? targetView.transform.position + Vector3.down * 0.35f      // chest-ish, just under the eyes
        : target.position + Vector3.up * 0.5f;

    private bool CanSeeTarget(bool ignoreRange = false)
    {
        Vector3 from = Muzzle, to = AimPoint;
        float dist = Vector3.Distance(from, to);
        if (!ignoreRange && dist > attackRange) return false;
        // Blocked by level geometry? (Ignore ourselves and the player.)
        if (Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform)) return true;
            return hit.collider.transform.IsChildOf(target);
        }
        return true;
    }

    private void Shoot()
    {
        Vector3 from = Muzzle;
        attackSpell.Fire(from, (AimPoint - from).normalized, transform, Faction.Enemy);
    }

    // ================================================================== damage
    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.source == Faction.Enemy) return;
        if (onlyReflectedDamage && !info.reflected) { hitFlash = 0.3f; return; }   // shrug it off
        // Style: a combo is a hit on someone already frozen / stunned by another spell; an airshot hits someone off the ground.
        bool combo = (IsFrozen || IsStunned) && info.freezeDuration <= 0f && info.stunDuration <= 0f;
        bool airborne = IsAirborne();
        if (info.slowAmount > 0f)
        {
            slowFactor = Mathf.Min(Time.time < slowUntil ? slowFactor : 1f, 1f - Mathf.Clamp01(info.slowAmount));
            slowUntil = Mathf.Max(slowUntil, Time.time + info.slowDuration);
        }
        if (info.knockback.sqrMagnitude > 0f && !rb.isKinematic && !IsFrozen)
        {
            rb.AddForce(info.knockback, ForceMode.VelocityChange);
            staggerUntil = Time.time + 0.45f + info.knockback.magnitude * 0.03f;   // big hits fly further
            dashUntil = 0f;
        }
        TakeDamage(info.amount);
        if (info.freezeDuration > 0f && !IsDead) Freeze(info.freezeDuration);   // the freezing hit itself isn't doubled
        if (info.stunDuration > 0f && !IsDead)
        {
            stunUntil = Mathf.Max(stunUntil, Time.time + info.stunDuration);
            windupStart = -1f;
            dashUntil = 0f;
        }
        if (StyleMeter.Instance != null) StyleMeter.Instance.EnemyHit(IsDead, info.reflected, combo, airborne);
    }

    // Hovering / turret enemies are kinematic and never count. The ray starts inside our own collider, so it skips it.
    private bool IsAirborne() =>
        !rb.isKinematic && !Physics.Raycast(new Vector3(capsule.bounds.center.x, capsule.bounds.min.y + 0.1f, capsule.bounds.center.z), Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore);

    public void Freeze(float duration)
    {
        if (!frozenApplied)
        {
            savedConstraints = rb.constraints;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            frozenApplied = true;
        }
        windupStart = -1f;   // the charging attack is lost
        dashUntil = 0f;
        frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
    }

    private void UpdateFreeze()
    {
        if (!frozenApplied || IsFrozen) return;
        rb.constraints = savedConstraints;
        frozenApplied = false;
        nextShotTime = Mathf.Max(nextShotTime, Time.time + 0.5f);
    }

    // "FROZEN" / "STUNNED" over the enemy's head.
    private void OnGUI()
    {
        string status = IsFrozen ? "FROZEN" : IsStunned ? "STUNNED" : null;
        if (status == null || IsDead) return;
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 sp = cam.WorldToScreenPoint(transform.position + Vector3.up * 2.9f);
        if (sp.z <= 0f) return;
        float ui = Screen.height / 1080f;
        if (frozenStyle == null) frozenStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        frozenStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(260f / sp.z, 16f, 34f) * ui);
        var r = new Rect(sp.x - 120f * ui, Screen.height - sp.y - 20f * ui, 240f * ui, 40f * ui);
        frozenStyle.normal.textColor = new Color(0f, 0.1f, 0.25f, 0.85f);
        GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), status, frozenStyle);
        frozenStyle.normal.textColor = IsFrozen ? new Color(0.7f, 0.95f, 1f) : new Color(1f, 0.9f, 0.3f);
        GUI.Label(r, status, frozenStyle);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        if (IsFrozen) amount *= frozenDamageMultiplier;
        Health -= amount;
        hitFlash = 1f;
        if (Health <= 0f) Die();
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        All.Remove(this);
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    // ================================================================== visual feedback
    private void UpdateColors()
    {
        if (renderers == null || baseColors == null || block == null) return;   // e.g. after a hot reload
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 6f);

        float charge = Winding ? Mathf.Clamp01((Time.time - windupStart) / (CurrentWindup / SpeedScale)) : 0f;

        if (telegraphRenderer != null)
        {
            // Pulse faster as the attack gets closer.
            float pulse = charge > 0f ? 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 40f, charge)) : 0f;
            telegraphRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, Color.Lerp(telegraphBase, telegraphColor * 2f, charge * pulse));
            telegraphRenderer.SetPropertyBlock(block);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r == telegraphRenderer) continue;
            r.GetPropertyBlock(block);
            Color baseCol = IsFrozen ? Color.Lerp(baseColors[i], FrozenTint, 0.85f)
                          : Time.time < slowUntil ? Color.Lerp(baseColors[i], SlowTint, 0.6f) : baseColors[i];
            block.SetColor(ColorId, Color.Lerp(baseCol, Color.white, hitFlash));
            r.SetPropertyBlock(block);
        }
    }
}
