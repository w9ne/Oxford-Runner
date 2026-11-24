using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Player hit points. Enemy projectiles damage it; parries heal it. On death the player respawns at
/// the last checkpoint with full health (tutorial / levels with checkpoints) or the scene restarts.
/// </summary>
public class PlayerHealth : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 100f;
    [Tooltip("Brief invulnerability after taking a hit so overlapping blasts don't double-dip.")]
    [SerializeField] private float invulnerabilityTime = 0.25f;

    public event Action<DamageInfo> Damaged;
    public event Action<float> Healed;
    public event Action Died;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float Normalized => Health / maxHealth;
    public bool IsDead { get; private set; }
    public Faction Faction => Faction.Player;

    private float invulnerableUntil;
    private FirstPersonController controller;
    private PlayerParry parry;

    private void Awake()
    {
        Health = maxHealth;
        controller = GetComponent<FirstPersonController>();
        parry = GetComponent<PlayerParry>();
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || info.source == Faction.Player || Time.time < invulnerableUntil) return;
        if (parry != null && parry.WindowOpen) return;   // parry barrier is up: blocks splash damage too
        Health = Mathf.Max(0f, Health - info.amount);
        invulnerableUntil = Time.time + invulnerabilityTime;
        Damaged?.Invoke(info);
        if (Health <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f) return;
        float before = Health;
        Health = Mathf.Min(maxHealth, Health + amount);
        if (Health > before) Healed?.Invoke(Health - before);
    }

    /// <summary>Used by scene transitions to carry health between levels.</summary>
    public void SetHealth(float value)
    {
        Health = Mathf.Clamp(value, 0f, maxHealth);
        IsDead = Health <= 0f;
    }

    private void Die()
    {
        IsDead = true;
        Died?.Invoke();

        if (Checkpoints.HasCheckpoint && controller != null)
        {
            Checkpoints.Respawn(controller);
            Health = maxHealth;
            IsDead = false;
        }
        else
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
