using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Moves straight forward, sweeping a sphere each frame so fast projectiles never tunnel through
/// walls or characters. On hit, damages every target of the opposing faction in the impact radius
/// and spawns impact VFX. The visual child (particles) is detached on impact so trails fade out.
///
/// Projectiles copy their stats from the spell at launch so they can be changed per-shot,
/// e.g. when the player parries one and it flies back faster and harder.
/// </summary>
public class SpellProjectile : MonoBehaviour
{
    [SerializeField] private Transform visual;         // particle effect root
    [SerializeField] private float visualFadeTime = 1f;

    private static readonly HashSet<SpellProjectile> active = new HashSet<SpellProjectile>();
    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private static readonly Collider[] overlapBuffer = new Collider[32];

    /// <summary>All projectiles currently in flight (used by the parry to find incoming shots).</summary>
    public static IReadOnlyCollection<SpellProjectile> Active => active;

    private ProjectileSpell spell;
    private Transform owner;
    private float dieTime;
    private bool done;
    private int piercesLeft;
    private readonly HashSet<IDamageable> pierced = new HashSet<IDamageable>();
    private IDamageable homingTarget;
    private float launchTime;
    private IDamageable lockedTarget;   // missile lock-on: chases this one anywhere, through cover

    public Faction Faction { get; private set; }
    public float Speed { get; private set; }
    public float Damage { get; private set; }
    public float ImpactRadius { get; private set; }
    public float HitRadius { get; private set; }
    public bool Parryable { get; private set; }
    private GameObject impactOverride;
    public bool WasReflected { get; private set; }

    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);

    public void Launch(ProjectileSpell spellData, Transform caster, Faction faction)
    {
        spell = spellData;
        owner = caster;
        Faction = faction;
        Speed = spell.speed;
        Damage = spell.damage;
        ImpactRadius = spell.impactRadius;
        HitRadius = spell.hitRadius;
        Parryable = spell.parryable;
        dieTime = Time.time + spell.lifetime;
        piercesLeft = spell.pierce;
        launchTime = Time.time;
        if (spell.homingTurnRate > 0f) homingTarget = PickHomingTarget();
    }

    /// <summary>Locks this projectile onto a target (missile lock-on): it homes in wherever the target goes
    /// (a chosen target is followed with no range / cone / sight checks), and lives long enough to get there.</summary>
    public void ForceTarget(IDamageable t)
    {
        homingTarget = t;
        lockedTarget = t;
        dieTime = Time.time + Mathf.Max(spell.lifetime, 8f);
    }

    /// <summary>Ends the projectile without exploding (e.g. erased by Arcane Nova).</summary>
    public void Fizzle()
    {
        if (!done) Finish(false, transform.position, Vector3.up);
    }

    // The opposing target closest to the flight direction, within range and the homing cone.
    private IDamageable PickHomingTarget()
    {
        IDamageable best = null;
        float bestAngle = spell.homingConeAngle;
        foreach (var d in Targeting.Opponents(transform.position, spell.homingRange, Faction))
        {
            Vector3 to = Targeting.Center(d) - transform.position;
            float angle = Vector3.Angle(transform.forward, to);
            if (angle > bestAngle || !Targeting.Clear(transform.position, Targeting.Center(d), owner, ((Component)d).transform)) continue;
            best = d;
            bestAngle = angle;
        }
        return best;
    }

    private void Steer()
    {
        if (homingTarget == null || (Component)homingTarget == null || homingTarget.IsDead)
        {
            lockedTarget = null;   // target gone: back to normal homing (and normal collisions)
            homingTarget = PickHomingTarget();
            if (homingTarget == null) return;
        }
        Vector3 aim = Targeting.Center(homingTarget);
        bool locked = lockedTarget != null && homingTarget == lockedTarget;
        if (locked)
        {
            // Arc over cover: aim above the target while far away, dive in when close.
            Vector3 flat = aim - transform.position; flat.y = 0f;
            aim += Vector3.up * Mathf.Clamp(flat.magnitude * 0.3f, 0f, 6f);
        }
        Vector3 to = aim - transform.position;
        if (to.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(to);
        // Turn harder the longer it flies, so missiles never orbit a target.
        float baseRate = WasReflected && locked ? 720f : spell.homingTurnRate;   // parried bolts have no homing of their own
        float rate = baseRate * (1f + (Time.time - launchTime) * 4f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, rate * Time.deltaTime);
    }

    private DamageInfo MakeDamage(Vector3 point)
    {
        var info = new DamageInfo(Damage, Faction, point, WasReflected);
        info.slowAmount = spell.slowAmount;
        info.slowDuration = spell.slowDuration;
        info.freezeDuration = spell.freezeDuration;
        if (spell.knockback > 0f) info.knockback = (transform.forward + Vector3.up * 0.3f).normalized * spell.knockback;
        return info;
    }

    /// <summary>
    /// Sends the projectile back the other way (parry). It now belongs to <paramref name="newOwner"/>,
    /// can't be parried again, and gets a speed / damage boost plus a colour change.
    /// </summary>
    public void Reflect(Vector3 position, Vector3 direction, Transform newOwner, Faction newFaction,
                        float speedMultiplier, float damageMultiplier, Color tint, GameObject impactVfx = null)
    {
        if (impactVfx != null) impactOverride = impactVfx;   // parried bolts blow up instead of fizzling
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        owner = newOwner;
        Faction = newFaction;
        Speed *= speedMultiplier;
        Damage *= damageMultiplier;
        ImpactRadius = Mathf.Max(ImpactRadius * 1.25f, 2.5f);   // a real blast
        Parryable = false;
        WasReflected = true;
        homingTarget = null;
        dieTime = Time.time + spell.lifetime;

        if (visual != null)
        {
            visual.localScale *= 1.4f;
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>())
            {
                var main = ps.main;
                main.startColor = tint;
            }
            foreach (var l in visual.GetComponentsInChildren<Light>()) l.color = tint;
        }

        // Solid parts (e.g. an enemy bolt's glowing core and trail) take the tint too.
        var block = new MaterialPropertyBlock();
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
        {
            mr.GetPropertyBlock(block);
            block.SetColor("_Color", tint);
            mr.SetPropertyBlock(block);
            mr.transform.localScale *= 1.3f;
        }
        foreach (var tr in GetComponentsInChildren<TrailRenderer>())
        {
            tr.startColor = new Color(tint.r, tint.g, tint.b, 0.9f);
            tr.endColor = new Color(tint.r, tint.g, tint.b, 0f);
        }
    }

    private void Update()
    {
        if (done || spell == null) return;

        if (Time.time >= dieTime) { Finish(false, transform.position, Vector3.up); return; }

        if ((spell.homingTurnRate > 0f && !WasReflected) || lockedTarget != null) Steer();

        float step = Speed * Time.deltaTime;
        Vector3 dir = transform.forward;

        // Enemy shot meeting a raised parry barrier: the barrier is a sphere around the player's chest,
        // so the bolt bounces off out in front where you can see it, not at your face.
        if (Parryable && PlayerParry.Instance != null && PlayerParry.Instance.WindowOpen
            && HitsSphere(transform.position, dir, step + HitRadius, PlayerParry.Instance.BarrierCenter,
                          PlayerParry.Instance.BarrierRadius, out Vector3 contact)
            && PlayerParry.Instance.TryCatch(this, contact)) return;

        if (FindHit(dir, step, out RaycastHit hit))
        {
            // Piercing shots pass through living targets, hurting each once, and only stop at walls
            // (or once they run out of pierces).
            if (piercesLeft > 0)
            {
                var victim = hit.collider.GetComponentInParent<IDamageable>();
                if (victim != null && !victim.IsDead && victim.Faction != Faction)
                {
                    pierced.Add(victim);
                    piercesLeft--;
                    DealAreaDamage(hit.point);
                    SpawnImpactVfx(hit.point, hit.normal);
                    return;   // next frame's sweep ignores this victim and carries on from here
                }
            }

            // Enemy shot reaching a player whose parry window is open: bounce instead of exploding.
            if (Parryable)
            {
                var parry = hit.collider.GetComponentInParent<PlayerParry>();
                if (parry != null && parry.TryCatch(this, hit.point)) return;
            }

            transform.position += dir * hit.distance;   // sphere centre at contact, not the surface point
            Finish(true, hit.point, hit.normal);
            return;
        }
        transform.position += dir * step;
    }

    // Does the segment start + dir * [0, length] enter the sphere? Returns the entry point.
    private static bool HitsSphere(Vector3 start, Vector3 dir, float length, Vector3 center, float radius, out Vector3 point)
    {
        point = start;
        Vector3 m = start - center;
        float c = m.sqrMagnitude - radius * radius;
        if (c <= 0f) return true;                       // already inside
        float b = Vector3.Dot(m, dir);
        if (b > 0f) return false;                       // heading away
        float disc = b * b - c;
        if (disc < 0f) return false;
        float t = -b - Mathf.Sqrt(disc);
        if (t > length) return false;
        point = start + dir * t;
        return true;
    }

    private bool FindHit(Vector3 dir, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.SphereCastNonAlloc(transform.position, HitRadius, dir, hitBuffer, distance,
                                               ~0, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var h = hitBuffer[i];
            if (owner != null && h.collider.transform.IsChildOf(owner)) continue;  // don't hit the caster
            if (lockedTarget != null && !lockedTarget.IsDead && (Component)lockedTarget != null
                && !h.collider.transform.IsChildOf(((Component)lockedTarget).transform)) continue;  // locked: only the target stops it
            if (pierced.Count > 0)
            {
                var d = h.collider.GetComponentInParent<IDamageable>();
                if (d != null && pierced.Contains(d)) continue;                     // already passed through
            }
            // distance 0 means we started overlapping; use our own position as the hit point
            if (h.distance == 0f) { h.point = transform.position; h.normal = -dir; }
            if (h.distance < best) { best = h.distance; closest = h; found = true; }
        }
        return found;
    }

    private void Finish(bool exploded, Vector3 point, Vector3 normal)
    {
        done = true;

        if (exploded)
        {
            DealAreaDamage(point);
            SpawnImpactVfx(point, normal);
        }

        // Let the trail fade instead of vanishing.
        if (visual != null)
        {
            visual.SetParent(null, true);
            // Local-space particles (the glowing ball) would freeze at the impact point: clear them.
            // Only world-space ones (the trail left in the air) fade out.
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(false, ps.main.simulationSpace == ParticleSystemSimulationSpace.World
                    ? ParticleSystemStopBehavior.StopEmitting : ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var l in visual.GetComponentsInChildren<Light>()) l.enabled = false;
            Destroy(visual.gameObject, visualFadeTime);
        }
        Destroy(gameObject);
    }

    private void SpawnImpactVfx(Vector3 point, Vector3 normal)
    {
        var prefab = impactOverride != null ? impactOverride : spell.impactVfx;
        if (prefab == null) return;
        var fx = Instantiate(prefab, point + normal * 0.1f, Quaternion.identity);
        Destroy(fx, impactOverride != null ? 3.5f : spell.impactVfxLifetime);
    }

    private void DealAreaDamage(Vector3 point)
    {
        int count = Physics.OverlapSphereNonAlloc(point, ImpactRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<IDamageable>();
        var info = MakeDamage(point);
        for (int i = 0; i < count; i++)
        {
            var target = overlapBuffer[i].GetComponentInParent<IDamageable>();
            if (target == null || target.IsDead || target.Faction == Faction) continue;
            if (damaged.Add(target)) target.TakeDamage(info);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, ImpactRadius > 0f ? ImpactRadius : 1f);
    }
}
