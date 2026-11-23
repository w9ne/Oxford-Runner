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

    public DamageInfo(float amount, Faction source, Vector3 point, bool reflected = false)
    {
        this.amount = amount;
        this.source = source;
        this.point = point;
        this.reflected = reflected;
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
