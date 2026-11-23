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

    public Faction Faction { get; private set; }
    public float Speed { get; private set; }
    public float Damage { get; private set; }
    public float ImpactRadius { get; private set; }
    public float HitRadius { get; private set; }
    public bool Parryable { get; private set; }
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
    }

    /// <summary>
    /// Sends the projectile back the other way (parry). It now belongs to <paramref name="newOwner"/>,
    /// can't be parried again, and gets a speed / damage boost plus a colour change.
    /// </summary>
    public void Reflect(Vector3 position, Vector3 direction, Transform newOwner, Faction newFaction,
                        float speedMultiplier, float damageMultiplier, Color tint)
    {
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        owner = newOwner;
        Faction = newFaction;
        Speed *= speedMultiplier;
        Damage *= damageMultiplier;
        ImpactRadius *= 1.25f;
        Parryable = false;
        WasReflected = true;
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

        float step = Speed * Time.deltaTime;
        Vector3 dir = transform.forward;

        if (FindHit(dir, step, out RaycastHit hit))
        {
            // Enemy shot reaching a player whose parry window is open: bounce instead of exploding.
            if (Parryable)
            {
                var parry = hit.collider.GetComponentInParent<PlayerParry>();
                if (parry != null && parry.TryCatch(this)) return;
            }

            transform.position = hit.point;
            Finish(true, hit.point, hit.normal);
            return;
        }
        transform.position += dir * step;
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
            if (spell.impactVfx != null)
            {
                var fx = Instantiate(spell.impactVfx, point + normal * 0.1f, Quaternion.identity);
                Destroy(fx, spell.impactVfxLifetime);
            }
        }

        // Let the trail fade instead of vanishing.
        if (visual != null)
        {
            visual.SetParent(null, true);
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>())
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            foreach (var l in visual.GetComponentsInChildren<Light>()) l.enabled = false;
            Destroy(visual.gameObject, visualFadeTime);
        }
        Destroy(gameObject);
    }

    private void DealAreaDamage(Vector3 point)
    {
        int count = Physics.OverlapSphereNonAlloc(point, ImpactRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        var damaged = new HashSet<IDamageable>();
        var info = new DamageInfo(Damage, Faction, point, WasReflected);
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
