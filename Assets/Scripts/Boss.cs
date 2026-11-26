using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The final boss: your shadow. Rides on an Enemy (health, damage, freeze / stun, lock-on, style) with
/// brainControlled on; this script moves and fights. BossFight wakes it up and resets it.
/// Routine: Range (keep 10-18 m, fireball / missiles) -> Close in and Arcane Blast -> 40% Charge (3 s magic
/// circle, then every shot at once) or back to Range. Always: dashes every ~1 s, slides sideways under
/// incoming spells, double-jumps now and then, and every 10 s raises a parry for 1.2 s. Its parried shots
/// can be parried back: that stuns it for 5 s.
/// </summary>
[RequireComponent(typeof(Enemy))]
public class Boss : MonoBehaviour
{
    private enum State { Asleep, Range, CloseIn, Blast, Charge, Dead }

    [Header("Spells (runtime copies are tuned for the boss)")]
    public ProjectileSpell fireball;
    public ProjectileSpell missiles;
    public NovaSpell blast;

    [Header("Effects")]
    public GameObject chargeCircleVfx;
    public GameObject releaseVfx;
    public GameObject parryShieldVfx;
    public GameObject smallBlastVfx;
    public GameObject bigBlastVfx;

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float keepMin = 10f, keepMax = 18f;
    public float dashSpeed = 22f, dashDuration = 0.22f, dashCooldown = 2.4f;
    public float slideSpeed = 17f, slideDuration = 0.45f, slideCooldown = 1.6f;
    public float jumpSpeed = 9f;

    [Header("Attacks")]
    public float shotInterval = 1.3f;
    public float rangeTime = 7f;
    public float blastRadius = 6f, blastDamage = 25f, blastWindup = 0.8f;
    public float chargeTime = 3f, chargeChance = 0.4f;

    [Header("Parry")]
    public float parryEvery = 10f, parryWindow = 1.2f, parryRadius = 2.6f, counterStun = 5f;
    public Color parryTint = new Color(1f, 0.15f, 0.2f);

    public bool IsDead => state == State.Dead;

    private Enemy enemy;
    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Transform player, eye, body;
    private ProjectileSpell myFireball, myMissiles;
    private readonly HashSet<SpellProjectile> myParries = new HashSet<SpellProjectile>();
    private State state;
    private Vector3 home, arenaCenter, moveVel;
    private float arenaRadius, stateUntil, nextShot, nextDash, dashUntil, nextSlide, slideUntil, nextJump, secondJumpAt;
    private float nextParry, parryUntil, strafeSign = 1f, nextStrafeFlip;
    private float capsuleHeight, bodyScaleY;
    private bool shotToggle;
    private GameObject chargeFx, shieldFx;
    private Animator anim;
    private static readonly int MoveXHash = Animator.StringToHash("MoveX"), MoveYHash = Animator.StringToHash("MoveY"), RunSpeedHash = Animator.StringToHash("RunSpeed");

    private void Awake()
    {
        enemy = GetComponent<Enemy>();
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        body = transform.Find("Body");
        capsuleHeight = capsule.height;
        bodyScaleY = body != null ? body.localScale.y : 1f;
        anim = body != null ? body.GetComponent<Animator>() : null;
        home = transform.position;
        // Runtime copies: parryable, softer hits, missiles that can be outrun.
        myFireball = Instantiate(fireball);
        myFireball.parryable = true;
        myFireball.damage = 12f;
        myMissiles = Instantiate(missiles);
        myMissiles.parryable = true;
        myMissiles.damage = 6f;
        myMissiles.homingTurnRate = 60f;
        myMissiles.lockOn = false;
        enemy.invulnerable = true;   // until the health bar has filled
        enemy.Died += _ => StartCoroutine(DeathSequence());
    }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        player = p.transform;
        var cam = p.GetComponentInChildren<Camera>();
        eye = cam != null ? cam.transform : player;
        if (PlayerParry.Instance != null) PlayerParry.Instance.Parried += OnPlayerParried;
    }

    private void OnDestroy()
    {
        if (PlayerParry.Instance != null) PlayerParry.Instance.Parried -= OnPlayerParried;
    }

    /// <summary>Starts the fight (after the health bar has filled).</summary>
    public void Wake(Vector3 center, float radius)
    {
        arenaCenter = center;
        arenaRadius = radius;
        enemy.invulnerable = false;
        Enter(State.Range);
        nextParry = Time.time + parryEvery;
        nextDash = Time.time + 1f;
    }

    /// <summary>Back to the start, full health, asleep (the player died in the fight).</summary>
    public void ResetFight()
    {
        StopAllCoroutines();
        EndCharge();
        EndSlide();
        if (shieldFx != null) Destroy(shieldFx);
        enemy.ResetState();
        enemy.invulnerable = true;
        rb.linearVelocity = Vector3.zero;
        rb.position = home;
        transform.position = home;
        state = State.Asleep;
    }

    private void Enter(State s)
    {
        state = s;
        stateUntil = Time.time + (s == State.Range ? rangeTime * Random.Range(0.8f, 1.2f) : s == State.CloseIn ? 6f : s == State.Blast ? blastWindup : chargeTime);
        if (s == State.Blast) StartBlastWindup();
        if (s == State.Charge) StartCharge();
    }

    // ================================================================== brain
    private void Update()
    {
        Animate();
        if (state == State.Asleep || state == State.Dead || player == null || GameFlow.PlayerDead) return;
        UpdateParry();
        if (enemy.IsFrozen || enemy.IsStunned)
        {
            if (state == State.Charge || state == State.Blast) { EndCharge(); Enter(State.Range); }   // a stun breaks the big attack
            return;
        }
        float dist = FlatTo(player.position).magnitude;
        switch (state)
        {
            case State.Range:
                if (Time.time > nextShot && CanSee()) { Shoot(shotToggle ? myMissiles : myFireball); shotToggle = !shotToggle; nextShot = Time.time + shotInterval; }
                if (Time.time > stateUntil) Enter(State.CloseIn);
                break;
            case State.CloseIn:
                if (dist < blastRadius * 0.6f) Enter(State.Blast);
                else if (Time.time > stateUntil) Enter(State.Range);
                break;
            case State.Blast:
                if (Time.time > stateUntil) { Blast(); Enter(Random.value < chargeChance ? State.Charge : State.Range); }
                break;
            case State.Charge:
                if (Time.time > stateUntil) { Release(); Enter(State.Range); }
                break;
        }
        UpdateDodges();
    }

    // Dash constantly, slide under incoming spells, double-jump now and then.
    private void UpdateDodges()
    {
        bool planted = state == State.Blast || state == State.Charge;
        if (planted) return;
        bool grounded = Grounded();
        var shot = IncomingShot();
        if (shot != null && Time.time > nextSlide && grounded)
        {
            Vector3 side = Vector3.Cross(Vector3.up, shot.transform.forward).normalized;
            if (Vector3.Dot(side, FlatTo(transform.position - shot.transform.position)) < 0f) side = -side;
            StartSlide(Clear(side) ? side : -side);
            return;
        }
        if (shot != null && Time.time > nextJump && grounded) { Jump(); return; }
        if (Time.time > nextDash && Time.time > slideUntil) StartDash(DashDirection());
        if (grounded && Time.time > nextJump && Random.value < 0.25f * Time.deltaTime) Jump();
        if (secondJumpAt > 0f && Time.time > secondJumpAt) { secondJumpAt = 0f; rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpSpeed, rb.linearVelocity.z); }
    }

    private Vector3 DashDirection()
    {
        Vector3 to = FlatTo(player.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, to) * (Random.value < 0.5f ? 1f : -1f);
        Vector3 dir = state == State.CloseIn ? (to + side * 0.6f).normalized          // zig-zag in
                    : FlatTo(player.position).magnitude < keepMin ? (-to + side * 0.5f).normalized
                    : side;
        return Clear(dir) ? dir : Clear(-dir) ? -dir : Vector3.zero;
    }

    private void FixedUpdate()
    {
        if (state == State.Asleep || state == State.Dead || player == null) return;
        if (GameFlow.PlayerDead || enemy.IsFrozen || enemy.IsStunned) return;   // Enemy already holds it still
        Vector3 to = FlatTo(player.position);
        float dist = to.magnitude;
        Vector3 dir = dist > 0.01f ? to / dist : transform.forward;
        Vector3 wish;
        if (Time.time < slideUntil || Time.time < dashUntil) wish = moveVel;
        else if (state == State.Blast || state == State.Charge) wish = Vector3.zero;
        else
        {
            if (Time.time > nextStrafeFlip) { strafeSign = -strafeSign; nextStrafeFlip = Time.time + Random.Range(1f, 2.5f); }
            Vector3 strafe = Vector3.Cross(Vector3.up, dir) * strafeSign;
            Vector3 w = state == State.CloseIn ? dir
                      : dist < keepMin ? (-dir + strafe * 0.5f).normalized
                      : dist > keepMax ? (dir + strafe * 0.5f).normalized : strafe;
            Vector3 fromCenter = FlatTo(transform.position, arenaCenter);   // stay off the walls
            if (fromCenter.magnitude > arenaRadius - 5f) w = (w - fromCenter.normalized * 1.5f).normalized;
            wish = w * moveSpeed;
        }
        rb.linearVelocity = new Vector3(wish.x, rb.linearVelocity.y, wish.z);
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, Quaternion.LookRotation(dir), 12f * Time.fixedDeltaTime));
    }

    // ================================================================== moves
    private void StartDash(Vector3 dir)
    {
        nextDash = Time.time + dashCooldown * Random.Range(0.7f, 1.3f);
        if (dir == Vector3.zero) return;
        moveVel = dir * dashSpeed;
        dashUntil = Time.time + dashDuration;
    }

    private void StartSlide(Vector3 dir)
    {
        nextSlide = Time.time + slideCooldown;
        moveVel = dir * slideSpeed;
        slideUntil = Time.time + slideDuration;
        capsule.height = capsuleHeight * 0.5f;
        capsule.center = Vector3.down * capsuleHeight * 0.25f;
        if (body != null) body.localScale = new Vector3(body.localScale.x, bodyScaleY * 0.5f, body.localScale.z);
        CancelInvoke(nameof(EndSlide));
        Invoke(nameof(EndSlide), slideDuration);
    }

    private void EndSlide()
    {
        capsule.height = capsuleHeight;
        capsule.center = Vector3.zero;
        if (body != null) body.localScale = new Vector3(body.localScale.x, bodyScaleY, body.localScale.z);
    }

    private void Jump()
    {
        nextJump = Time.time + Random.Range(2.5f, 4.5f);
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpSpeed, rb.linearVelocity.z);
        secondJumpAt = Time.time + 0.4f;   // double jump near the top
    }

    // ================================================================== attacks
    private Vector3 Muzzle => transform.position + transform.forward * 0.8f + Vector3.up * 0.6f;
    private Vector3 AimPoint => eye.position + Vector3.down * 0.35f;

    private void Shoot(ProjectileSpell spell, float yaw = 0f)
    {
        Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * (AimPoint - Muzzle).normalized;
        var p = spell.Fire(Muzzle, dir, transform, Faction.Enemy);
        if (spell != myFireball || p == null) return;
        foreach (var ps in p.GetComponentsInChildren<ParticleSystem>())   // thinner flames so you can see past it
        {
            var em = ps.emission;
            em.rateOverTimeMultiplier *= 0.35f;
            em.rateOverDistanceMultiplier *= 0.35f;
        }
    }

    private void StartBlastWindup()
    {
        Sfx.Tail(Sfx.Bank.blastCharge, blastWindup, transform.position);
        if (blast == null || blast.vfx == null) return;
        var fx = Instantiate(blast.vfx, Feet(), Quaternion.identity);
        fx.transform.localScale *= blast.vfxScale * blastRadius / blast.radius;
        var shift = fx.AddComponent<ParticleTimeShift>();   // same trick as the player's nova: burst lands on the damage
        shift.follow = transform;
        shift.followOffset = Feet() - transform.position;
        shift.chargeSpeed = Mathf.Max(1f, blast.vfxChargeTime / blastWindup);
        shift.chargeDuration = blastWindup;
        Destroy(fx, blast.vfxLifetime + blastWindup);
    }

    private void Blast()
    {
        Vector3 c = transform.position;
        foreach (var d in Targeting.Opponents(c, blastRadius, Faction.Enemy))
            if (Targeting.Clear(c, Targeting.Center(d), transform, ((Component)d).transform))
                d.TakeDamage(new DamageInfo(blastDamage, Faction.Enemy, Targeting.Center(d)));
        foreach (var p in new List<SpellProjectile>(SpellProjectile.Active))   // erases your shots too
            if (p != null && p.Faction == Faction.Player && (p.transform.position - c).sqrMagnitude < blastRadius * blastRadius) p.Fizzle();
        Sfx.At(Sfx.Bank.blastBoom, c, 1f, 0f);
        float near = player != null ? Mathf.Clamp01(1f - FlatTo(player.position).magnitude / 25f) : 0.5f;
        CameraShake.Shake(Mathf.Lerp(2f, 6f, near), Mathf.Lerp(0.4f, 0.8f, near));   // the closer you are, the harder it hits
        var flash = new GameObject("BossBlastFlash").AddComponent<FadingLight>();
        flash.transform.position = c + Vector3.up * 1.5f;
        flash.Begin(parryTint, blastRadius * 1.8f, 10f, 0.5f);
    }

    private void StartCharge()
    {
        Sfx.Tail(Sfx.Bank.blastCharge, chargeTime, transform.position);
        if (chargeCircleVfx == null) return;
        chargeFx = Instantiate(chargeCircleVfx, Feet() + Vector3.up * 0.05f, Quaternion.identity);
        chargeFx.transform.localScale *= 4f;
    }

    private void EndCharge()
    {
        if (chargeFx != null) Destroy(chargeFx);
    }

    // Everything at once: a fan of fireballs and two volleys of missiles.
    private void Release()
    {
        EndCharge();
        if (releaseVfx != null) Destroy(Instantiate(releaseVfx, transform.position, Quaternion.identity), 4f);
        Sfx.At(Sfx.Bank.blastBoom, transform.position, 1f, 0f);
        CameraShake.Shake(5f, 0.8f);
        foreach (float yaw in new[] { -14f, 0f, 14f }) Shoot(myFireball, yaw);
        Shoot(myMissiles, -25f);
        Shoot(myMissiles, 25f);
    }

    // ================================================================== parry
    private void UpdateParry()
    {
        if (Time.time > nextParry && !enemy.IsStunned)
        {
            nextParry = Time.time + parryEvery;
            parryUntil = Time.time + parryWindow;
            Sfx.At(Sfx.Bank.parryRaise, transform.position, 0.8f);
            if (parryShieldVfx != null) { shieldFx = Instantiate(parryShieldVfx, transform); shieldFx.transform.localPosition = Vector3.down; Destroy(shieldFx, parryWindow); }
        }
        if (Time.time > parryUntil) return;
        foreach (var p in new List<SpellProjectile>(SpellProjectile.Active))
        {
            if (p == null || p.Faction != Faction.Player) continue;
            Vector3 at = p.transform.position;
            if ((at - transform.position).sqrMagnitude > parryRadius * parryRadius) continue;
            p.Reflect(at, (AimPoint - at).normalized, transform, Faction.Enemy, 1.5f, 1f, parryTint, null, parryable: true);
            myParries.Add(p);
            Sfx.At(Sfx.Bank.parry, at, 0.9f);
        }
    }

    // You parried one of its parries: stunned.
    private void OnPlayerParried(SpellProjectile p)
    {
        if (!myParries.Remove(p) || state == State.Dead) return;
        enemy.Stun(counterStun);
        if (state == State.Charge || state == State.Blast) { EndCharge(); Enter(State.Range); }
    }

    // ================================================================== death
    // No death animation on a capsule: a few small blasts, then one big one.
    private IEnumerator DeathSequence()
    {
        state = State.Dead;
        EndCharge();
        rb.isKinematic = true;
        for (int i = 0; i < 5; i++)
        {
            if (smallBlastVfx != null) Destroy(Instantiate(smallBlastVfx, transform.position + Random.insideUnitSphere * 0.8f, Quaternion.identity), 3f);
            Sfx.At(Sfx.Bank.enemyDeath, transform.position, 1f, 0.15f);
            CameraShake.Shake(1.5f, 0.25f);
            yield return new WaitForSeconds(0.45f);
        }
        if (bigBlastVfx != null) Destroy(Instantiate(bigBlastVfx, transform.position, Quaternion.identity), 5f);
        Sfx.At(Sfx.Bank.blastBoom, transform.position, 1f, 0f);
        CameraShake.Shake(5f, 0.8f);
        foreach (var r in GetComponentsInChildren<Renderer>()) r.enabled = false;
        capsule.enabled = false;
        yield return new WaitForSeconds(1.5f);
        BossFight.Won();
        Destroy(gameObject);
    }

    // Same locomotion blend as the player's body (PlayerBody): run clips by local velocity, legs still while sliding.
    private void Animate()
    {
        if (anim == null) return;
        Vector3 v = transform.InverseTransformDirection(rb.isKinematic ? Vector3.zero : rb.linearVelocity);
        Vector2 planar = new Vector2(v.x, v.z);
        float speed = planar.magnitude;
        Vector2 blend = speed > 0.2f && Time.time > slideUntil && !enemy.IsStunned ? planar / Mathf.Max(speed, 5.5f) : Vector2.zero;
        anim.SetFloat(MoveXHash, blend.x, 0.1f, Time.deltaTime);
        anim.SetFloat(MoveYHash, blend.y, 0.1f, Time.deltaTime);
        anim.SetFloat(RunSpeedHash, Grounded() ? Mathf.Clamp(speed / 5.5f, 1f, 1.8f) : 0.35f);
        anim.speed = enemy.IsFrozen ? 0f : 1f;
    }

    // ================================================================== helpers
    private static Vector3 FlatTo(Vector3 to, Vector3 from) { Vector3 d = to - from; d.y = 0f; return d; }
    private Vector3 FlatTo(Vector3 to) => FlatTo(to, transform.position);

    private Vector3 Feet() =>
        Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore) ? hit.point : transform.position + Vector3.down;

    private bool Grounded() => Physics.Raycast(transform.position, Vector3.down, capsuleHeight * 0.5f + 0.15f, ~0, QueryTriggerInteraction.Ignore);

    private bool Clear(Vector3 dir) => !Physics.SphereCast(transform.position, 0.4f, dir, out _, 5f, ~0, QueryTriggerInteraction.Ignore);

    private bool CanSee() => Targeting.Clear(Muzzle, AimPoint, transform, player);

    // A player shot within 10 m heading at us.
    private SpellProjectile IncomingShot()
    {
        foreach (var p in SpellProjectile.Active)
        {
            if (p == null || p.Faction != Faction.Player) continue;
            Vector3 toMe = transform.position - p.transform.position;
            if (toMe.sqrMagnitude < 100f && Vector3.Dot(p.transform.forward, toMe.normalized) > 0.9f) return p;
        }
        return null;
    }
}
