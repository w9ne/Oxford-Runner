using UnityEngine;

/// <summary>
/// A spell that launches one or more projectiles and explodes on impact, damaging every opposing
/// target inside a small radius. Used for player spells and enemy attacks.
/// Optional behaviours, all tuned per asset: multishot fan, piercing, homing, slow and knockback.
///   Fireball        - single straight shot, small area blast.
///   Frost Lance     - fast, pierces several enemies, slows each one it passes through.
///   Arcane Missiles - a fan of weak missiles that home in on nearby enemies.
/// </summary>
[CreateAssetMenu(menuName = "Spells/Projectile Spell", fileName = "NewProjectileSpell")]
public class ProjectileSpell : Spell
{
    [Header("Projectile")]
    public SpellProjectile projectilePrefab;
    public float speed = 35f;
    public float lifetime = 3f;          // seconds before it fizzles out
    public float hitRadius = 0.25f;      // thickness of the projectile for collision
    [Tooltip("Can the player parry this projectile back? (Enemy shots only.)")]
    public bool parryable = true;

    [Header("Multishot")]
    [Min(1)] public int projectileCount = 1;
    [Tooltip("Total fan angle (degrees) the projectiles are spread across.")]
    public float spreadAngle = 0f;

    [Header("Piercing")]
    [Tooltip("How many targets the projectile passes through before stopping (0 = stops at the first).")]
    [Min(0)] public int pierce = 0;

    [Header("Homing")]
    [Tooltip("Turn rate in degrees per second toward the chosen target (0 = flies straight).")]
    public float homingTurnRate = 0f;
    public float homingRange = 40f;
    [Tooltip("Only targets within this angle of the launch direction are chosen.")]
    public float homingConeAngle = 60f;

    [Header("Lock-on (player)")]
    [Tooltip("Uses the MissileLockOn on the caster: hold the crosshair on an enemy to lock, shots then chase it anywhere.")]
    public bool lockOn = false;
    public float lockTime = 0.35f;
    public float lockRange = 70f;
    [Tooltip("How close to the crosshair (degrees) an enemy must be to start locking.")]
    public float lockConeAngle = 10f;

    [Header("Effects on hit")]
    [Range(0f, 1f)] public float slowAmount = 0f;
    public float slowDuration = 0f;
    public float knockback = 0f;
    [Tooltip("Seconds the target is frozen solid (can't move or act, takes extra damage).")]
    public float freezeDuration = 0f;

    [Header("Impact")]
    public float damage = 15f;
    public float impactRadius = 2f;      // small area of effect
    public GameObject impactVfx;
    public float impactVfxLifetime = 2.5f;

    public override void Cast(SpellCaster caster, Vector3 origin, Vector3 direction)
    {
        IDamageable locked = null;
        if (lockOn)
        {
            var lockOnComp = caster.GetComponent<MissileLockOn>();
            if (lockOnComp != null) locked = lockOnComp.LockedTarget;
        }
        if (locked != null)
        {
            // Launch up and toward the target in a fan; homing does the rest.
            Vector3 to = (Targeting.Center(locked) - origin).normalized;
            direction = (to + Vector3.up * 0.35f).normalized;
        }
        Fire(origin, direction, caster.transform, Faction.Player, locked);
    }

    /// <summary>
    /// Spawns and launches the projectile(s). Used by the player's SpellCaster and by enemies.
    /// Returns the first projectile.
    /// </summary>
    public SpellProjectile Fire(Vector3 origin, Vector3 direction, Transform owner, Faction faction, IDamageable lockedTarget = null)
    {
        if (projectilePrefab == null) { Debug.LogWarning($"{name}: no projectile prefab assigned."); return null; }
        Quaternion aim = Quaternion.LookRotation(direction);
        SpellProjectile first = null;
        for (int i = 0; i < projectileCount; i++)
        {
            // Spread evenly across the fan, with a slight upward arc on the outer shots so they look launched.
            float t = projectileCount > 1 ? (float)i / (projectileCount - 1) - 0.5f : 0f;
            Quaternion rot = aim * Quaternion.Euler(-Mathf.Abs(t) * spreadAngle * 0.3f, t * spreadAngle, 0f);
            SpellProjectile p = Instantiate(projectilePrefab, origin, rot);
            p.Launch(this, owner, faction);
            if (lockedTarget != null) p.ForceTarget(lockedTarget);
            if (first == null) first = p;
        }
        return first;
    }
}
