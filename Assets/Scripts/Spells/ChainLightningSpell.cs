using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightning Strike (asset still named Chain Lightning): a bolt drops from the sky onto the spot under
/// the crosshair and bursts in a blue blast that hurts every enemy around it. Aim-assisted: if an
/// enemy is near the aimed spot, the bolt hits that enemy. Weaker than Arcane Nova, short cooldown.
/// </summary>
[CreateAssetMenu(menuName = "Spells/Lightning Strike", fileName = "NewLightningStrike")]
public class ChainLightningSpell : Spell
{
    [Header("Strike")]
    public float range = 70f;
    public float damage = 24f;
    public float blastRadius = 4.5f;
    [Tooltip("Enemies this close to the aimed spot get struck directly.")]
    public float aimAssistRadius = 4f;
    public float skyHeight = 30f;
    public float knockback = 6f;
    public float stunDuration = 1f;

    [Header("Legacy (unused)")]
    [Min(0)] public int jumps = 0;
    public float jumpRadius = 0f;
    [Range(0f, 1f)] public float damageFalloff = 1f;

    [Header("Visuals")]
    public Color boltColor = new Color(0.45f, 0.7f, 1f);
    public float boltWidth = 0.35f;
    public GameObject hitVfx;
    public float hitVfxLifetime = 2f;
    public float hitVfxScale = 1f;

    private static readonly RaycastHit[] hits = new RaycastHit[16];

    public override void Cast(SpellCaster caster, Vector3 origin, Vector3 direction)
    {
        Transform owner = caster.transform;
        Vector3 spot = AimSpot(origin, direction, owner);

        // Snap to the enemy nearest the aimed spot.
        IDamageable snapped = null;
        float best = aimAssistRadius * aimAssistRadius;
        foreach (var d in Targeting.Opponents(spot, aimAssistRadius, Faction.Player))
        {
            float dist = (Targeting.Center(d) - spot).sqrMagnitude;
            if (dist < best) { best = dist; snapped = d; }
        }
        if (snapped != null) spot = GroundBelow(Targeting.Center(snapped));

        // Blast: everyone in the radius (walls between the strike and the target block it).
        Vector3 blastCenter = spot + Vector3.up * 0.8f;
        foreach (var d in Targeting.Opponents(blastCenter, blastRadius, Faction.Player))
        {
            Vector3 at = Targeting.Center(d);
            if (!Targeting.Clear(blastCenter, at, null, ((Component)d).transform)) continue;
            var info = new DamageInfo(damage, Faction.Player, at);
            info.stunDuration = stunDuration;
            Vector3 away = at - spot; away.y = 0f;
            if (knockback > 0f) info.knockback = ((away.sqrMagnitude > 0.01f ? away.normalized : Vector3.zero) + Vector3.up).normalized * knockback;
            d.TakeDamage(info);
        }

        var points = new List<Vector3> { spot + Vector3.up * skyHeight + Random.insideUnitSphere * 3f, spot };
        LightningArc.Spawn(points, boltColor, boltWidth, 2.2f, 18f);
        if (hitVfx != null)
        {
            var fx = Instantiate(hitVfx, spot, Quaternion.identity);
            fx.transform.localScale *= hitVfxScale;
            Destroy(fx, hitVfxLifetime);
        }
    }

    // Where the crosshair lands (first thing hit), or the ground under the end of the range.
    private Vector3 AimSpot(Vector3 origin, Vector3 dir, Transform owner)
    {
        int n = Physics.RaycastNonAlloc(origin, dir, hits, range, ~0, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        Vector3 point = origin + dir * range;
        for (int i = 0; i < n; i++)
        {
            if (hits[i].collider.transform.IsChildOf(owner)) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; point = hits[i].point; }
        }
        return bestDist == float.MaxValue ? GroundBelow(point) : point;
    }

    private static Vector3 GroundBelow(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore)
            ? hit.point : p;
    }
}

/// <summary>A short-lived jagged line between points that flickers and fades (used by Chain Lightning).</summary>
public class LightningArc : MonoBehaviour
{
    private const float SegmentLength = 0.6f;
    private float life = 0.18f, jitter = 0.25f, lightIntensity = 4f;

    private static Material material;
    private LineRenderer line;
    private List<Vector3> anchors;
    private Color color;
    private float born;
    private Light flash;

    public static void Spawn(List<Vector3> points, Color color) => Spawn(points, color, 0.1f, 1f, 8f);

    /// <summary>Thicker / longer-lived bolts (sky strike): width in metres, life multiplier, flash range.</summary>
    public static void Spawn(List<Vector3> points, Color color, float width, float lifeScale, float lightRange)
    {
        if (points.Count < 2) return;
        var go = new GameObject("LightningArc");
        var arc = go.AddComponent<LightningArc>();
        arc.anchors = points;
        arc.color = color;
        arc.born = Time.time;

        if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "LightningArc" };
        arc.line = go.AddComponent<LineRenderer>();
        arc.line.sharedMaterial = material;
        arc.life = 0.18f * lifeScale;
        arc.jitter = 0.25f * Mathf.Max(1f, width / 0.1f * 0.5f);
        arc.line.widthCurve = new AnimationCurve(new Keyframe(0f, width * 0.8f), new Keyframe(1f, width * 1.4f));
        arc.line.numCapVertices = 2;
        arc.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        arc.line.receiveShadows = false;

        go.transform.position = points[points.Count - 1];
        arc.flash = go.AddComponent<Light>();
        arc.flash.type = LightType.Point;
        arc.flash.color = color;
        arc.flash.range = lightRange;
        arc.lightIntensity = lightRange > 10f ? 8f : 4f;
        arc.flash.intensity = arc.lightIntensity;

        arc.Rebuild();
    }

    private void Update()
    {
        float t = (Time.time - born) / life;
        if (t >= 1f) { Destroy(gameObject); return; }
        Rebuild();   // re-jitter every frame so it crackles
        float a = 1f - t;
        line.startColor = new Color(1f, 1f, 1f, a);
        line.endColor = new Color(color.r, color.g, color.b, a);
        flash.intensity = lightIntensity * a;
    }

    private void Rebuild()
    {
        var pts = new List<Vector3>();
        for (int i = 0; i < anchors.Count - 1; i++)
        {
            Vector3 a = anchors[i], b = anchors[i + 1];
            int segs = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / SegmentLength));
            for (int s = 0; s < segs; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)s / segs);
                if (s > 0) p += Random.insideUnitSphere * jitter;
                pts.Add(p);
            }
        }
        pts.Add(anchors[anchors.Count - 1]);
        line.positionCount = pts.Count;
        line.SetPositions(pts.ToArray());
    }
}
