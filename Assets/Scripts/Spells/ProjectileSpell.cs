using UnityEngine;

/// <summary>
/// A spell that launches a projectile in a straight line and explodes on impact,
/// damaging every enemy inside a small radius. (Spell 1: Fireball)
/// </summary>
[CreateAssetMenu(menuName = "Spells/Projectile Spell", fileName = "NewProjectileSpell")]
public class ProjectileSpell : Spell
{
    [Header("Projectile")]
    public SpellProjectile projectilePrefab;
    public float speed = 35f;
    public float lifetime = 3f;          // seconds before it fizzles out
    public float hitRadius = 0.25f;      // thickness of the projectile for collision

    [Header("Impact")]
    public float damage = 15f;
    public float impactRadius = 2f;      // small area of effect
    public GameObject impactVfx;
    public float impactVfxLifetime = 2.5f;

    public override void Cast(SpellCaster caster, Vector3 origin, Vector3 direction)
    {
        if (projectilePrefab == null) { Debug.LogWarning($"{name}: no projectile prefab assigned."); return; }
        SpellProjectile p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction));
        p.Launch(this, caster.transform);
    }
}
