using UnityEngine;

/// <summary>
/// Moves straight forward, sweeping a sphere each frame so fast projectiles never tunnel through
/// walls or enemies. On hit, damages all enemies in the spell's impact radius and spawns impact VFX.
/// The visual child (particles) is detached on impact so trails fade out instead of popping.
/// </summary>
public class SpellProjectile : MonoBehaviour
{
    [SerializeField] private Transform visual;         // particle effect root
    [SerializeField] private float visualFadeTime = 1f;

    private ProjectileSpell spell;
    private Transform owner;
    private float dieTime;
    private bool done;

    private static readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private static readonly Collider[] overlapBuffer = new Collider[32];

    public void Launch(ProjectileSpell spellData, Transform caster)
    {
        spell = spellData;
        owner = caster;
        dieTime = Time.time + spell.lifetime;
    }

    private void Update()
    {
        if (done || spell == null) return;

        if (Time.time >= dieTime) { Finish(false, transform.position, Vector3.up); return; }

        float step = spell.speed * Time.deltaTime;
        Vector3 dir = transform.forward;

        if (FindHit(dir, step, out RaycastHit hit))
        {
            transform.position = hit.point;
            Finish(true, hit.point, hit.normal);
            return;
        }
        transform.position += dir * step;
    }

    private bool FindHit(Vector3 dir, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.SphereCastNonAlloc(transform.position, spell.hitRadius, dir, hitBuffer, distance,
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
        int count = Physics.OverlapSphereNonAlloc(point, spell.impactRadius, overlapBuffer, ~0, QueryTriggerInteraction.Ignore);
        var damaged = new System.Collections.Generic.HashSet<Enemy>();
        for (int i = 0; i < count; i++)
        {
            var enemy = overlapBuffer[i].GetComponentInParent<Enemy>();
            if (enemy != null && damaged.Add(enemy)) enemy.TakeDamage(spell.damage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (spell == null) return;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, spell.impactRadius);
    }
}
