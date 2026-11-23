using System;
using UnityEngine;

/// <summary>
/// Basic enemy: walks toward the player (Rigidbody-driven) and has health.
/// Stats are set by the GameManager when spawned so difficulty can scale per wave.
/// Shooting spells will be added later; fireInterval is stored for that.
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class Enemy : MonoBehaviour
{
    [SerializeField] private float maxHealth = 30f;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float stoppingDistance = 8f;   // ranged casters keep some distance
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float fireInterval = 3f;       // used once enemy spells are added

    public event Action<Enemy> Died;

    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public float FireInterval => fireInterval;
    public bool IsDead { get; private set; }

    private Rigidbody rb;
    private Transform target;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        Health = maxHealth;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    public void Configure(float health, float speed, float interval)
    {
        maxHealth = health;
        Health = health;
        moveSpeed = speed;
        fireInterval = interval;
    }

    private void FixedUpdate()
    {
        if (IsDead || target == null) return;

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;
        float dist = toTarget.magnitude;
        Vector3 dir = dist > 0.01f ? toTarget / dist : transform.forward;

        Vector3 v = rb.linearVelocity;
        Vector3 horizontal = dist > stoppingDistance ? dir * moveSpeed : Vector3.zero;
        rb.linearVelocity = new Vector3(horizontal.x, v.y, horizontal.z);

        Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, look, turnSpeed * Time.fixedDeltaTime));
    }

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health -= amount;
        if (Health <= 0f) Die();
    }

    public void Die()
    {
        if (IsDead) return;
        IsDead = true;
        Died?.Invoke(this);
        Destroy(gameObject);
    }
}
