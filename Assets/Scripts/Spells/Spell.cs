using UnityEngine;

/// <summary>How the left hand moves when a spell is cast (see PlayerBody).</summary>
public enum CastGesture
{
    Thrust,   // palm shoves forward (fireball)
    Sweep,    // hand slashes across from the left (frost lance)
    Point,    // arm reaches out high and crackles (chain lightning)
    Flurry,   // quick little circles, one per missile (arcane missiles)
    Slam,     // hand lifts, then slams down (nova)
}

/// <summary>
/// Base class for every spell in the player's arsenal.
/// Each spell is a ScriptableObject asset so new spells are created and tuned in the Inspector.
/// </summary>
public abstract class Spell : ScriptableObject
{
    public string displayName = "Spell";
    [Min(0f)] public float cooldown = 0.7f;
    public Color uiColor = Color.white;
    [Tooltip("Left-hand motion when this spell is cast.")]
    public CastGesture gesture = CastGesture.Thrust;

    [Header("Casting")]
    [Tooltip("Wind-up before the spell goes off (0 = instant).")]
    [Min(0f)] public float castTime = 0f;
    [Tooltip("Player move speed multiplier while winding up.")]
    [Range(0f, 1f)] public float castMoveMultiplier = 1f;

    /// <summary>Called when a wind-up starts (only for spells with a castTime).</summary>
    public virtual void BeginCast(SpellCaster caster) { }

    /// <summary>Fire the spell from origin toward direction.</summary>
    public abstract void Cast(SpellCaster caster, Vector3 origin, Vector3 direction);
}
