using System;
using UnityEngine;

/// <summary>
/// Stationary practice target. Bobs in place, flashes when hit, pops when destroyed.
/// </summary>
public class TrainingDummy : MonoBehaviour, IDamageable, IHealthReadout
{
    [SerializeField] private float maxHealth = 15f;   // one fireball
    [SerializeField] private Color hitColor = new Color(1f, 0.35f, 0.2f);
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 1.6f;

    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public event Action<TrainingDummy> Died;
    public bool IsDead { get; private set; }
    public Faction Faction => Faction.Enemy;
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;

    private Renderer[] renderers;
    private MaterialPropertyBlock block;
    private Vector3 basePos;
    private float flash, deathTime = -1f, phase;
    private Color baseColor = Color.white;

    private void Awake()
    {
        Health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        if (renderers.Length > 0 && renderers[0].sharedMaterial != null && renderers[0].sharedMaterial.HasProperty(ColorId))
            baseColor = renderers[0].sharedMaterial.GetColor(ColorId);
        basePos = transform.position;
        phase = UnityEngine.Random.value * Mathf.PI * 2f;
        if (GetComponent<WorldHealthBar>() == null) gameObject.AddComponent<WorldHealthBar>();
    }

    public void TakeDamage(DamageInfo info) => TakeDamage(info.amount);

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health -= amount;
        flash = 1f;
        if (Health <= 0f)
        {
            IsDead = true;
            deathTime = Time.time;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            Died?.Invoke(this);
        }
    }

    private void Update()
    {
        if (IsDead)
        {
            // Quick "pop": swell then shrink away.
            float t = (Time.time - deathTime) / 0.35f;
            float s = t < 0.3f ? Mathf.Lerp(1f, 1.3f, t / 0.3f) : Mathf.Lerp(1.3f, 0f, (t - 0.3f) / 0.7f);
            transform.localScale = Vector3.one * Mathf.Max(s, 0f);
            if (t >= 1f) Destroy(gameObject);
            return;
        }

        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed + phase) * bobHeight);
        transform.Rotate(0f, 40f * Time.deltaTime, 0f);

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 5f);
        block.SetColor(ColorId, Color.Lerp(baseColor, hitColor, flash));
        foreach (var r in renderers) r.SetPropertyBlock(block);
    }
}
