using UnityEngine;

/// <summary>
/// A small health bar floating above a character, always facing the camera.
/// Reads any <see cref="IHealthReadout"/> on the same object (enemies, training dummies).
/// Built from three unlit quads at runtime, so it needs no prefab or canvas:
/// dark background, a yellow "recent damage" trail that drains behind the red fill, and the fill itself.
/// Enemies also get a "FROZEN" / "STUNNED" label above the bar.
/// </summary>
public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private float width = 1.1f;
    [SerializeField] private float height = 0.13f;
    [Tooltip("Gap between the top of the model and the bar.")]
    [SerializeField] private float clearance = 0.35f;
    [SerializeField] private Color fillColor = new Color(0.9f, 0.18f, 0.2f);
    [SerializeField] private Color trailColor = new Color(1f, 0.85f, 0.45f);
    [SerializeField] private Color backColor = new Color(0f, 0f, 0f, 0.75f);

    private static Material sharedMaterial;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private IHealthReadout target;
    private Transform root, fill, trail;
    private float heightAboveOrigin, trailValue = 1f;
    private Camera cam;
    private Enemy enemy;
    private TextMesh status;

    private void Start()
    {
        target = GetComponent<IHealthReadout>();
        if (target == null) { enabled = false; return; }

        // Sit just above the tallest renderer (measured once, before the bar itself exists).
        float top = transform.position.y + 2f;
        bool any = false;
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer || r.name == "ChargeOrb") continue;
            top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            any = true;
        }
        heightAboveOrigin = top - transform.position.y + clearance;

        root = new GameObject("HealthBar").transform;
        root.SetParent(transform, false);
        Quad("Back", backColor, 0f, 1f + 0.06f / width, 1f + 0.06f / height, 0);
        trail = Quad("Trail", trailColor, -0.001f, 1f, 1f, 1);
        fill = Quad("Fill", fillColor, -0.002f, 1f, 1f, 2);
        enemy = target as Enemy;
        if (enemy != null)
        {
            status = new GameObject("Status").AddComponent<TextMesh>();
            status.transform.SetParent(root, false);
            status.transform.localPosition = new Vector3(0f, 0.32f, 0f);
            status.anchor = TextAnchor.MiddleCenter;
            status.fontSize = 64;
            status.characterSize = 0.05f;
            status.fontStyle = FontStyle.Bold;
            status.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            status.GetComponent<MeshRenderer>().sharedMaterial = status.font.material;
        }
        LateUpdate();
    }

    private Transform Quad(string name, Color color, float z, float sx, float sy, int order)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        DestroyImmediate(go.GetComponent<Collider>());   // now, before it is parented under a Rigidbody
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Material();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.sortingOrder = order;   // draw back -> trail -> fill regardless of distance sorting
        var block = new MaterialPropertyBlock();
        block.SetColor(ColorId, color);
        mr.SetPropertyBlock(block);

        // A pivot on the left edge so the fill can shrink toward the left by scaling x.
        var pivot = new GameObject(name + "Pivot").transform;
        pivot.SetParent(root, false);
        pivot.localPosition = new Vector3(-width * 0.5f * sx, 0f, z);
        go.transform.SetParent(pivot, false);
        go.transform.localPosition = new Vector3(width * 0.5f * sx, 0f, 0f);
        go.transform.localScale = new Vector3(width * sx, height * sy, 1f);
        return pivot;
    }

    private static Material Material()
    {
        if (sharedMaterial == null)
        {
            // Sprites/Default is always included in builds, supports alpha and ignores lighting.
            sharedMaterial = new Material(Shader.Find("Sprites/Default")) { name = "WorldHealthBar" };
            sharedMaterial.renderQueue = 3100;   // over transparent effects
        }
        return sharedMaterial;
    }

    private void LateUpdate()
    {
        if (root == null) return;
        if (target.IsDead) { root.gameObject.SetActive(false); return; }

        float h = target.MaxHealth > 0f ? Mathf.Clamp01(target.Health / target.MaxHealth) : 0f;
        trailValue = h > trailValue ? h : Mathf.MoveTowards(trailValue, h, Time.deltaTime * 0.8f);
        fill.localScale = new Vector3(Mathf.Max(h, 0.0001f), 1f, 1f);
        trail.localScale = new Vector3(Mathf.Max(trailValue, 0.0001f), 1f, 1f);
        if (status != null)
        {
            status.text = enemy.IsFrozen ? "FROZEN" : enemy.IsStunned ? "STUNNED" : "";
            status.color = enemy.IsFrozen ? new Color(0.7f, 0.95f, 1f) : new Color(1f, 0.9f, 0.3f);
        }

        // Undo the parent's scale (dummies pop by scaling) and face the camera.
        Vector3 ps = transform.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(ps.x, 0.001f), 1f / Mathf.Max(ps.y, 0.001f), 1f / Mathf.Max(ps.z, 0.001f));
        root.position = transform.position + Vector3.up * heightAboveOrigin;
        if (cam == null) cam = Camera.main;
        if (cam != null) root.rotation = Quaternion.LookRotation(root.position - cam.transform.position, cam.transform.up);
    }
}
