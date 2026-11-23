using UnityEngine;

/// <summary>
/// Base class for every spell in the player's arsenal.
/// Each spell is a ScriptableObject asset so new spells are created and tuned in the Inspector.
/// </summary>
public abstract class Spell : ScriptableObject
{
    public string displayName = "Spell";
    [Min(0f)] public float cooldown = 0.7f;
    public Color uiColor = Color.white;

    /// <summary>Fire the spell from origin toward direction.</summary>
    public abstract void Cast(SpellCaster caster, Vector3 origin, Vector3 direction);
}
