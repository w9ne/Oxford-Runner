using System;
using UnityEngine;

/// <summary>
/// Basic caster enemy: walks toward the player (Rigidbody-driven), keeps some distance, and
/// periodically fires a parryable bolt. Each shot is telegraphed by a wind-up glow so the player
/// can time a parry. Stats are set by the GameManager when spawned so difficulty scales per wave.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class Enemy : MonoBehaviour, IDamageable
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Stats")]
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 8f;   // ranged casters keep some distance
    [SerializeField] private float turnSpeed = 8f;
    [Tooltip("If true, only parried (reflected) projectiles can hurt this enemy.")]
    [SerializeField] private bool onlyReflectedDamage;

    [Header("Attack")]
    [SerializeField] private ProjectileSpell attackSpell;
    [SerializeField] private float fireInterval = 3f;
    [SerializeField] private float windupTime = 0.6f;       // telegraph before each shot
    [SerializeField] private float attackRange = 35f;
    [SerializeField] private Vector3 muzzleOffset = new Vector3(0f, 1.1f, 0.7f);
    [SerializeField] private Renderer telegraphRenderer;    // glows during wind-up (the "Face")
    [SerializeField] private Color telegraphColor = new Color(1f, 0.2f, 0.9f);
    [Tooltip("Size of the glowing orb that grows at the muzzle during the wind-up.")]
    [SerializeField] private float chargeOrbSize = 0.55f;

    public event Action<Enemy> Died;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float FireInterval => fireInterval;
    public bool IsDead { get; private set; }
    public Faction Faction => Faction.Enemy;

    private Rigidbody rb;
    private Transform target;
    private Camera targetView;
    private float nextShotTime, windupStart = -1f;
    private MaterialPropertyBlock block;
    private Color telegraphBase = Color.white;
    private float hitFlash;
    private Renderer[] renderers;
    private Color[] baseColors;
    private Transform chargeOrb;
    private Light chargeLight;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
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
    }

    // A glowing orb that swells at the muzzle while winding up, so every shot is clearly telegraphed.
    // Uses the bolt's own core material so it reads as "the shot that's coming".
    private void CreateChargeOrb()
    {
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "ChargeOrb";
        Destroy(orb.GetComponent<Collider>());
        orb.transform.SetParent(transform, false);
        orb.transform.localPosition = muzzleOffset;
        var mr = orb.GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var core = attackSpell != null && attackSpell.projectilePrefab != null ? attackSpell.projectilePrefab.transform.Find("Core") : null;
        if (core != null) mr.sharedMaterial = core.GetComponent<Renderer>().sharedMaterial;
        else mr.material.color = telegraphColor;

        chargeLight = orb.AddComponent<Light>();
        chargeLight.type = LightType.Point;
        chargeLight.color = telegraphColor;
        chargeLight.range = 4f;
        chargeLight.intensity = 0f;

        chargeOrb = orb.transform;
        chargeOrb.localScale = Vector3.zero;
        orb.SetActive(false);
    }

    private void Start()
    {
        if (attackSpell != null) CreateChargeOrb();
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            targetView = player.GetComponentInChildren<Camera>();
        }
        // Stagger the first shot so a wave doesn't fire in unison.
        nextShotTime = Time.time + fireInterval * UnityEngine.Random.Range(0.5f, 1.2f);
    }

    public void Configure(float health, float speed, float interval)
    {
        maxHealth = health;
        Health = health;
        moveSpeed = speed;
        fireInterval = interval;
    }

    private void FixedUpdate()
    {
        if (IsDead || target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;

        if (!rb.isKinematic)   // stationary turrets are kinematic and only rotate
        {
            Vector3 v = rb.linearVelocity;
            bool winding = windupStart >= 0f;
            Vector3 horizontal = dist > stoppingDistance && !winding ? dir * moveSpeed : Vector3.zero;   // plant feet to cast
            rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);
        }

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));
    }

    private void Update()
    {
        if (IsDead) return;
        UpdateAttack();
        UpdateColors();
    }

    // ---------- Attack ----------
    private void UpdateAttack()
    {
        if (attackSpell == null || target == null) return;

        if (windupStart < 0f)
        {
            if (Time.time >= nextShotTime && CanSeeTarget()) windupStart = Time.time;
            return;
        }

        if (Time.time - windupStart >= windupTime)
        {
            windupStart = -1f;
            nextShotTime = Time.time + fireInterval;
            if (CanSeeTarget()) Shoot();
        }
    }

    private Vector3 Muzzle => transform.TransformPoint(muzzleOffset);

    private Vector3 AimPoint => targetView != null
        ? targetView.transform.position + Vector3.down * 0.35f      // chest-ish, just under the eyes
        : target.position + Vector3.up * 0.5f;

    private bool CanSeeTarget()
    {
        Vector3 from = Muzzle, to = AimPoint;
        float dist = Vector3.Distance(from, to);
        if (dist > attackRange) return false;
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

    // ---------- Damage ----------
    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.source == Faction.Enemy) return;
        if (onlyReflectedDamage && !info.reflected) { hitFlash = 0.3f; return; }   // shrug it off
        TakeDamage(info.amount);
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health -= amount;
        hitFlash = 1f;
        if (Health <= 0f) Die();
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Died?.Invoke(this);
        Destroy(gameObject);
    }

    // ---------- Visual feedback ----------
    private void UpdateColors()
    {
        if (renderers == null || baseColors == null || block == null) return;   // e.g. after a hot reload
        hitFlash = Mathf.MoveTowards(hitFlash, 0f, Time.deltaTime * 6f);

        float charge = windupStart >= 0f ? Mathf.Clamp01((Time.time - windupStart) / windupTime) : 0f;
        if (chargeOrb != null)
        {
            bool charging = charge > 0f;
            if (chargeOrb.gameObject.activeSelf != charging) chargeOrb.gameObject.SetActive(charging);
            if (charging)
            {
                float wobble = 1f + 0.12f * Mathf.Sin(Time.time * Mathf.Lerp(12f, 40f, charge));
                chargeOrb.localScale = Vector3.one * (chargeOrbSize * Mathf.SmoothStep(0.15f, 1f, charge) * wobble);
                chargeLight.intensity = Mathf.Lerp(0.5f, 4f, charge);
            }
        }

        if (telegraphRenderer != null)
        {
            // Pulse faster as the shot gets closer.
            float pulse = charge > 0f ? 0.6f + 0.4f * Mathf.Sin(Time.time * Mathf.Lerp(10f, 40f, charge)) : 0f;
            telegraphRenderer.GetPropertyBlock(block);
            block.SetColor(ColorId, Color.Lerp(telegraphBase, telegraphColor * 2f, charge * pulse));
            telegraphRenderer.SetPropertyBlock(block);
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == telegraphRenderer) continue;
            r.GetPropertyBlock(block);
            block.SetColor(ColorId, Color.Lerp(baseColors[i], Color.white, hitFlash));
            r.SetPropertyBlock(block);
        }
    }
}
