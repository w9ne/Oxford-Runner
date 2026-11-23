/// <summary>
/// Anything that spells (player or enemy) can hurt: enemies, training dummies, later the player.
/// </summary>
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(float amount);
}
