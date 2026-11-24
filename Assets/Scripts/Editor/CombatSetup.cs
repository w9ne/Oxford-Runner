using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Combat Assets
/// Creates the enemy bolt (a magenta recolour of the fireball) and its spell asset, arms the Enemy
/// prefab with it, and gives the Player prefab health, parry and the HUD. Safe to re-run.
/// </summary>
public static class CombatSetup
{
    private const string FireballPrefab = "Assets/Prefabs/Spells/Fireball_Projectile.prefab";
    private const string BoltPrefab = "Assets/Prefabs/Spells/EnemyBolt_Projectile.prefab";
    private const string FireballSpell = "Assets/Spells/Spell1_Fireball.asset";
    private const string BoltSpell = "Assets/Spells/Enemy_Bolt.asset";
    private const string EnemyPrefab = "Assets/Prefabs/Enemy.prefab";
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";

    private static readonly Color BoltColor = new Color(1f, 0.2f, 0.85f);
    private const string CoreMaterial = "Assets/Materials/EnemyBolt_Core.mat";
    private const string TrailMaterial = "Assets/Materials/EnemyBolt_Trail.mat";

    [MenuItem("Oxford Runner/Setup Combat Assets")]
    public static void Setup()
    {
        var boltPrefab = CreateBoltPrefab();
        var boltSpell = CreateBoltSpell(boltPrefab);

        // Player shots can't be "parried" by anyone.
        var fireball = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(FireballSpell);
        if (fireball != null) { fireball.parryable = false; EditorUtility.SetDirty(fireball); }

        // Enemies fire bolts.
        EditPrefab(EnemyPrefab, root =>
        {
            var so = new SerializedObject(root.GetComponent<Enemy>());
            so.FindProperty("attackSpell").objectReferenceValue = boltSpell;
            so.FindProperty("maxHealth").floatValue = 50f;   // common enemy base HP
            var face = root.transform.Find("Face");
            if (face != null) so.FindProperty("telegraphRenderer").objectReferenceValue = face.GetComponent<Renderer>();
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // Player gets health, parry and HUD.
        EditPrefab(PlayerPrefab, root =>
        {
            var health = GetOrAdd<PlayerHealth>(root);
            var parry = GetOrAdd<PlayerParry>(root);
            var hud = GetOrAdd<PlayerHUD>(root);

            var cam = root.GetComponentInChildren<Camera>();
            var pso = new SerializedObject(parry);
            pso.FindProperty("view").objectReferenceValue = cam != null ? cam.transform : null;
            pso.FindProperty("speedMultiplier").floatValue = 2f;   // reflected shots fly 2x faster
            pso.ApplyModifiedPropertiesWithoutUndo();

            var hso = new SerializedObject(hud);
            hso.FindProperty("health").objectReferenceValue = health;
            hso.FindProperty("parry").objectReferenceValue = parry;
            hso.ApplyModifiedPropertiesWithoutUndo();
        });

        AssetDatabase.SaveAssets();
        Debug.Log("[CombatSetup] Enemy bolt created, enemies armed, player has health + parry + HUD.");
        VfxSetup.Apply();
    }

    private static GameObject CreateBoltPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefab) == null)
            AssetDatabase.CopyAsset(FireballPrefab, BoltPrefab);

        EditPrefab(BoltPrefab, root =>
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                Color c = main.startColor.color;
                // Keep each layer's brightness but shift the hue to magenta.
                float v = Mathf.Max(c.r, c.g, c.b);
                main.startColor = new Color(BoltColor.r * v, BoltColor.g * v, BoltColor.b * v, c.a);
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) { l.color = BoltColor; l.intensity = Mathf.Max(l.intensity, 3f); }
            AddCore(root);
        });
        return AssetDatabase.LoadAssetAtPath<GameObject>(BoltPrefab);
    }

    // A bright unlit orb + trail so the bolt reads clearly at any distance (parry needs readability).
    private static void AddCore(GameObject root)
    {
        var old = root.transform.Find("Core");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(root.transform, false);
        core.transform.localScale = Vector3.one * 0.4f;
        var mr = core.GetComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateMaterial(CoreMaterial, "Unlit/Color", new Color(1f, 0.45f, 0.95f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = core.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.widthMultiplier = 0.35f;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = GetOrCreateMaterial(TrailMaterial, "Sprites/Default", Color.white);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(BoltColor, 0f), new GradientColorKey(BoltColor, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material GetOrCreateMaterial(string path, string shader, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find(shader));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static ProjectileSpell CreateBoltSpell(GameObject boltPrefab)
    {
        var spell = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(BoltSpell);
        if (spell == null)
        {
            spell = ScriptableObject.CreateInstance<ProjectileSpell>();
            AssetDatabase.CreateAsset(spell, BoltSpell);
        }
        var fireball = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(FireballSpell);
        spell.displayName = "Enemy Bolt";
        spell.uiColor = BoltColor;
        spell.projectilePrefab = boltPrefab.GetComponent<SpellProjectile>();
        spell.speed = 20f;            // readable but snappy
        spell.lifetime = 5f;
        spell.hitRadius = 0.2f;       // matches the visible core, so it hits when it looks like it hits
        spell.damage = 10f;
        spell.impactRadius = 1.2f;
        spell.parryable = true;
        if (fireball != null) { spell.impactVfx = fireball.impactVfx; spell.impactVfxLifetime = fireball.impactVfxLifetime; }
        EditorUtility.SetDirty(spell);
        return spell;
    }

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            edit(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        var c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }
}
