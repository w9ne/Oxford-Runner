using UnityEngine;

/// <summary>
/// Arcane Nova: a blast centred on the caster. Damages and knocks back every enemy in the radius and
/// erases enemy projectiles caught in it, which makes it a panic button when parrying isn't an option.
/// Long cooldown. Has a short wind-up (castTime): the effect starts charging on the player the moment
/// the spell is cast, sped up so its burst lands exactly when the blast goes off.
/// </summary>
[CreateAssetMenu(menuName = "Spells/Nova", fileName = "NewNova")]
public class NovaSpell : Spell
{
    public float radius = 7f;
    public float damage = 58.5f;
    [Tooltip("Velocity change applied to enemies, pushing them away from the caster.")]
    public float knockback = 30f;
    public bool destroysProjectiles = true;

    [Header("Visuals")]
    public GameObject vfx;
    public float vfxLifetime = 3f;
    public float vfxScale = 1f;
    [Tooltip("When the effect's own burst happens (seconds into it at normal speed).")]
    public float vfxChargeTime = 1f;
    public Color flashColor = new Color(1f, 0.15f, 0.08f);

    public override void BeginCast(SpellCaster caster)
    {
        Sfx.Tail(Sfx.Bank.blastCharge, castTime + 0.1f);   // the end of the charge-up lands on the blast
        if (vfx != null) SpawnVfx(caster.transform, true);
    }

    // Effect at ground level under the caster. During a wind-up it rides along with the caster and its
    // charge-up plays fast so the burst lines up with the damage.
    private void SpawnVfx(Transform owner, bool windUp)
    {
        Vector3 at = owner.position;
        if (Physics.Raycast(at, Vector3.down, out RaycastHit hit, 3f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
        var fx = Instantiate(vfx, at, Quaternion.identity);
        fx.transform.localScale *= vfxScale;
        if (windUp && castTime > 0f)
        {
            var shift = fx.AddComponent<ParticleTimeShift>();
            shift.follow = owner;
            shift.followOffset = at - owner.position;
            shift.chargeSpeed = Mathf.Max(1f, vfxChargeTime / castTime);
            shift.chargeDuration = castTime;
        }
        Destroy(fx, vfxLifetime + castTime);
    }

    public override void Cast(SpellCaster caster, Vector3 origin, Vector3 direction)
    {
        Transform owner = caster.transform;
        Vector3 center = owner.position;

        foreach (var d in Targeting.Opponents(center, radius, Faction.Player))
        {
            Vector3 at = Targeting.Center(d);
            if (!Targeting.Clear(center, at, owner, ((Component)d).transform)) continue;   // walls block it
            Vector3 away = at - center;
            away.y = 0f;
            away = away.sqrMagnitude > 0.001f ? away.normalized : owner.forward;
            var info = new DamageInfo(damage, Faction.Player, at);
            info.knockback = (away + Vector3.up * 0.5f).normalized * knockback;
            d.TakeDamage(info);
        }

        if (destroysProjectiles)
        {
            float r2 = radius * radius;
            // Copy first: fizzling removes projectiles from the active set.
            var shots = new System.Collections.Generic.List<SpellProjectile>(SpellProjectile.Active);
            foreach (var p in shots)
                if (p != null && p.Faction != Faction.Player && (p.transform.position - center).sqrMagnitude <= r2)
                    p.Fizzle();
        }

        if (vfx != null && castTime <= 0f) SpawnVfx(owner, false);

        // Weight: the ground shakes and a red flash lights the whole blast.
        Sfx.Play(Sfx.Bank.blastBoom, 1f, 0f);
        CameraShake.Shake(6f, 0.9f);   // the ground heaves
        var flash = new GameObject("NovaFlash").AddComponent<FadingLight>();
        flash.transform.position = center + Vector3.up * 1.5f;
        flash.Begin(flashColor, radius * 1.8f, 12f, 0.6f);
    }
}

/// <summary>A point light that starts bright and fades out, then removes itself (Arcane Nova's red flash).</summary>
public class FadingLight : MonoBehaviour
{
    private Light lit;
    private float peak, life, born;

    public void Begin(Color color, float range, float intensity, float seconds)
    {
        lit = gameObject.AddComponent<Light>();
        lit.type = LightType.Point;
        lit.color = color;
        lit.range = range;
        lit.intensity = peak = intensity;
        life = seconds;
        born = Time.time;
    }

    private void Update()
    {
        float t = (Time.time - born) / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        lit.intensity = peak * (1f - t) * (1f - t);
    }
}
