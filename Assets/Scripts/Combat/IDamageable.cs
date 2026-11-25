using System.Collections.Generic;
using UnityEngine;

/// <summary>Which side something is on. Projectiles only damage the opposing faction.</summary>
public enum Faction { Player, Enemy }

/// <summary>Everything a target needs to know about a hit.</summary>
public struct DamageInfo
{
    public float amount;
    public Faction source;
    public bool reflected;      // true if this came from a parried projectile
    public Vector3 point;

    // Optional effects (zero = none). Targets that don't support an effect just ignore it.
    public float slowAmount;    // 0..1 fraction of speed removed (Frost Lance)
    public float slowDuration;  // seconds
    public Vector3 knockback;   // velocity change, m/s (Arcane Nova)
    public float freezeDuration; // seconds frozen solid (Frost Lance)
    public float stunDuration;   // seconds stopped in place (Lightning)

    public DamageInfo(float amount, Faction source, Vector3 point, bool reflected = false)
    {
        this.amount = amount;
        this.source = source;
        this.point = point;
        this.reflected = reflected;
        slowAmount = 0f;
        slowDuration = 0f;
        knockback = Vector3.zero;
        freezeDuration = 0f;
        stunDuration = 0f;
    }
}

/// <summary>
/// Anything spells can hurt: enemies, training dummies, the player.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    Faction Faction { get; }
    void TakeDamage(DamageInfo info);
}

/// <summary>Anything that shows a health bar (see <see cref="WorldHealthBar"/>).</summary>
public interface IHealthReadout
{
    float Health { get; }
    float MaxHealth { get; }
    bool IsDead { get; }
}

/// <summary>Helpers shared by spells that need to find targets.</summary>
public static class Targeting
{
    private static readonly Collider[] buffer = new Collider[1024];   // walls count too: Level 3 has ~700 colliders in 50 m

    /// <summary>Living damageables of the opposing faction within radius, each listed once.</summary>
    public static List<IDamageable> Opponents(Vector3 center, float radius, Faction attacker)
    {
        var found = new List<IDamageable>();
        int n = Physics.OverlapSphereNonAlloc(center, radius, buffer, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var d = buffer[i].GetComponentInParent<IDamageable>();
            if (d == null || d.IsDead || d.Faction == attacker || found.Contains(d)) continue;
            found.Add(d);
        }
        return found;
    }

    /// <summary>A good point to aim at on a target (middle of its colliders).</summary>
    public static Vector3 Center(IDamageable d)
    {
        var c = (Component)d;
        var col = c.GetComponentInChildren<Collider>();
        return col != null && col.enabled ? col.bounds.center : c.transform.position;
    }

    /// <summary>True if nothing solid (other than the two endpoints' own objects) blocks the line.</summary>
    public static bool Clear(Vector3 from, Vector3 to, Transform ignoreA, Transform ignoreB)
    {
        if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)) return true;
        var t = hit.collider.transform;
        return (ignoreA != null && t.IsChildOf(ignoreA)) || (ignoreB != null && t.IsChildOf(ignoreB));
    }
}
