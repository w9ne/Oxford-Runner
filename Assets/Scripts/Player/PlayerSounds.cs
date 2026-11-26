using UnityEngine;

/// <summary>
/// The player's sounds, read off the controller each frame (no hooks needed in the movement code): footsteps
/// (faster when running, on walls while wall-running), jump / double jump, landing (louder after a long fall),
/// dash, slide, wall-grab; plus hits (with a camera kick that scales with the damage), death, parry, casts and
/// respawns. Added to the player by Sfx.
/// </summary>
public class PlayerSounds : MonoBehaviour
{
    private FirstPersonController fpc;
    private Rigidbody rb;
    private bool wasGrounded = true, wasDashing, wasSliding, wasWallRunning;
    private float lastVy, nextStep, airborneSince, minVyInAir;

    private void Start()
    {
        fpc = GetComponent<FirstPersonController>();
        rb = GetComponent<Rigidbody>();
        var b = Sfx.Bank;
        var health = GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Damaged += info =>
            {
                Sfx.Play(b.playerHit, 1f);
                CameraShake.Shake(Mathf.Clamp(1.2f + info.amount * 0.12f, 1.5f, 5f), 0.3f);   // every hit kicks the view; big hits harder
            };
            health.Died += () => Sfx.Play(b.playerDeath, 1f, 0f);
        }
        var parry = GetComponent<PlayerParry>();
        if (parry != null)
        {
            parry.Parried += _ => Sfx.Play(b.parry, 1f, 0.03f);
            parry.WindowOpened += () => Sfx.Play(b.parryRaise, 0.35f);
        }
        var caster = GetComponent<SpellCaster>();
        if (caster != null) caster.Cast += spell => Sfx.Play(spell.castSound, 0.8f);
        Checkpoints.Respawned += OnRespawn;
    }

    private void OnDestroy() => Checkpoints.Respawned -= OnRespawn;
    private void OnRespawn() => Sfx.Play(Sfx.Bank.respawn, 0.7f, 0f);

    private void Update()
    {
        if (fpc == null || GameFlow.Paused) return;
        var b = Sfx.Bank;
        Vector3 v = rb.linearVelocity;
        float flat = new Vector2(v.x, v.z).magnitude;
        bool grounded = fpc.IsGrounded;

        if (fpc.IsDashing && !wasDashing) Sfx.Play(b.dash, 0.9f);
        if (fpc.IsSliding && !wasSliding) Sfx.Play(b.slide, 0.6f);
        if (fpc.IsWallRunning && !wasWallRunning) Sfx.Play(b.wallRun, 0.6f);

        if (!grounded && wasGrounded) airborneSince = Time.time;
        if (!grounded) minVyInAir = Mathf.Min(minVyInAir, v.y);
        // Jump: upward speed appears (off the ground, a double jump, or a wall jump); pitch rises for air jumps.
        if (v.y > 4f && v.y - lastVy > 4f) Sfx.Play(b.jump, 0.55f, 0f);
        if (grounded && !wasGrounded && Time.time - airborneSince > 0.25f)
        {
            Sfx.Play(b.land, Mathf.Clamp(0.25f - minVyInAir * 0.04f, 0.25f, 0.9f));
            nextStep = Time.time + 0.2f;
        }
        if (grounded) minVyInAir = 0f;

        bool stepping = (grounded && !fpc.IsSliding && flat > 2f) || (fpc.IsWallRunning && flat > 2f);
        if (stepping && Time.time >= nextStep)
        {
            Sfx.Play(b.footstep, 0.35f, 0.12f);
            nextStep = Time.time + Mathf.Clamp(2.2f / flat, 0.18f, 0.45f);   // ~0.27 s at a run, quicker on walls and dashes
        }

        wasGrounded = grounded;
        wasDashing = fpc.IsDashing;
        wasSliding = fpc.IsSliding;
        wasWallRunning = fpc.IsWallRunning;
        lastVy = v.y;
    }
}
