using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Spells
/// Creates the player's five spells (assets, projectile and impact prefabs) and gives them to the
/// Player prefab's SpellCaster, all unlocked. Projectiles are recoloured copies of the fireball;
/// Chain Lightning and Arcane Nova use the Kevin Iglesias spell effects. Safe to re-run.
///   1 Fireball        - existing, straight shot with a small blast.
///   2 Frost Lance     - fast, pierces up to 3 enemies, slows them by 50% for 2.5 s.
///   3 Chain Lightning - instant, strikes from the sky where you aim (stuns, small blast).
///   4 Arcane Missiles - 3 homing missiles.
///   5 Arcane Nova     - blast around you: damage, knockback, erases enemy bolts. 6 s cooldown.
/// </summary>
public static class SpellSetup
{
    private const string Prefabs = "Assets/Prefabs/Spells/";
    private const string Spells = "Assets/Spells/";
    private const string Materials = "Assets/Materials/";
    private const string KIFx = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Spells/";

    private static readonly Color FrostColor = new Color(0.55f, 0.9f, 1f);
    private static readonly Color LightningColor = new Color(1f, 0.95f, 0.55f);
    private static readonly Color ArcaneColor = new Color(0.45f, 0.5f, 1f);
    private static readonly Color NovaColor = new Color(0.85f, 0.95f, 1f);

    [MenuItem("Oxford Runner/Setup Spells")]
    public static void Setup()
    {
        var fireball = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(Spells + "Spell1_Fireball.asset");
        if (fireball == null) { Debug.LogError("[SpellSetup] Fireball spell missing."); return; }
        string fireballProjectile = AssetDatabase.GetAssetPath(fireball.projectilePrefab);
        string fireballImpact = AssetDatabase.GetAssetPath(fireball.impactVfx);

        // --- 2 Frost Lance
        var frostShot = RecolouredCopy(fireballProjectile, Prefabs + "FrostLance_Projectile.prefab", FrostColor, 0.8f,
            root => AddCore(root, "FrostLance", FrostColor, new Vector3(0.12f, 0.12f, 0.9f), PrimitiveType.Capsule, 0.15f));
        var frostImpact = RecolouredCopy(fireballImpact, Prefabs + "FrostLance_Impact.prefab", FrostColor, 0.7f, null);
        var frost = GetOrCreate<ProjectileSpell>(Spells + "Spell2_FrostLance.asset");
        frost.gesture = CastGesture.Sweep;
        frost.displayName = "Frost Lance";
        frost.uiColor = FrostColor;
        frost.cooldown = 0.9f;
        frost.projectilePrefab = frostShot.GetComponent<SpellProjectile>();
        frost.speed = 60f;
        frost.lifetime = 2f;
        frost.hitRadius = 0.2f;
        frost.parryable = false;
        frost.damage = 12f;
        frost.impactRadius = 0.6f;
        frost.impactVfx = frostImpact;
        frost.impactVfxLifetime = 1.5f;
        frost.pierce = 3;
        frost.slowAmount = 0f;
        frost.slowDuration = 0f;
        frost.freezeDuration = 2f;        // frozen solid: no moving / casting, takes 2.5x damage
        Dirty(frost);

        // --- 3 Chain Lightning
        var lightning = GetOrCreate<ChainLightningSpell>(Spells + "Spell3_ChainLightning.asset");
        lightning.gesture = CastGesture.Point;
        lightning.displayName = "Lightning Strike";
        lightning.uiColor = new Color(0.45f, 0.7f, 1f);
        lightning.cooldown = 0.8f;
        lightning.damage = 24f;
        lightning.blastRadius = 4.5f;
        lightning.aimAssistRadius = 4f;
        lightning.knockback = 6f;
        lightning.stunDuration = 1f;
        lightning.boltColor = new Color(0.45f, 0.7f, 1f);
        lightning.boltWidth = 0.35f;
        lightning.hitVfx = AssetDatabase.LoadAssetAtPath<GameObject>(KIFx + "Human_SpellAura_Lightning.prefab");
        lightning.hitVfxScale = 0.6f;
        lightning.hitVfxLifetime = 1.5f;
        Dirty(lightning);

        // --- 4 Arcane Missiles
        var missileShot = RecolouredCopy(fireballProjectile, Prefabs + "ArcaneMissile_Projectile.prefab", ArcaneColor, 0.55f,
            root => AddCore(root, "ArcaneMissile", ArcaneColor, Vector3.one * 0.18f, PrimitiveType.Sphere, 0.2f));
        var missileImpact = RecolouredCopy(fireballImpact, Prefabs + "ArcaneMissile_Impact.prefab", ArcaneColor, 0.5f, null);
        var missiles = GetOrCreate<ProjectileSpell>(Spells + "Spell4_ArcaneMissiles.asset");
        missiles.gesture = CastGesture.Flurry;
        missiles.displayName = "Arcane Missiles";
        missiles.uiColor = ArcaneColor;
        missiles.cooldown = 1.1f;
        missiles.projectilePrefab = missileShot.GetComponent<SpellProjectile>();
        missiles.lifetime = 3f;
        missiles.hitRadius = 0.2f;
        missiles.parryable = false;
        missiles.damage = 14f;
        missiles.impactRadius = 1.2f;
        missiles.impactVfx = missileImpact;
        missiles.impactVfxLifetime = 1.5f;
        missiles.projectileCount = 5;
        missiles.spreadAngle = 70f;
        missiles.speed = 30f;
        missiles.homingTurnRate = 450f;
        missiles.homingRange = 50f;
        missiles.homingConeAngle = 120f;
        missiles.lockOn = true;           // hold crosshair on an enemy: red [ ] -> green, missiles chase it anywhere
        missiles.lockTime = 0.35f;
        missiles.lockRange = 70f;
        missiles.lockConeAngle = 10f;
        Dirty(missiles);

        // --- 5 Arcane Nova
        var nova = GetOrCreate<NovaSpell>(Spells + "Spell5_ArcaneNova.asset");
        nova.gesture = CastGesture.Slam;
        nova.displayName = "Arcane Nova";
        nova.uiColor = NovaColor;
        nova.cooldown = 6f;
        nova.radius = 7f;
        nova.damage = 58.5f;      // 45 x 1.3
        nova.knockback = 30f;
        nova.castTime = 0.3f;
        nova.castMoveMultiplier = 0.25f;
        nova.destroysProjectiles = true;
        nova.vfx = AssetDatabase.LoadAssetAtPath<GameObject>(KIFx + "Human_Spell_Shockwave_Explosion.prefab");
        nova.vfxLifetime = 3f;
        nova.vfxScale = 1.4f;
        Dirty(nova);

        // --- Give the player all five.
        fireball.gesture = CastGesture.Thrust;
        Dirty(fireball);
        var list = new List<Spell> { fireball, frost, lightning, missiles, nova };
        var player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        try
        {
            var caster = player.GetComponent<SpellCaster>();
            var so = new SerializedObject(caster);
            var arr = so.FindProperty("spells");
            arr.arraySize = list.Count;
            for (int i = 0; i < list.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
            so.FindProperty("unlockedCount").intValue = list.Count;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (player.GetComponent<MissileLockOn>() == null) player.AddComponent<MissileLockOn>();
            PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        AssetDatabase.SaveAssets();
        Debug.Log("[SpellSetup] 5 spells created and given to the Player prefab (all unlocked).");
        VfxSetup.Apply();
    }

    // Copies a particle prefab and shifts every particle system and light to the given colour
    // (keeping each layer's brightness), optionally scaling the visuals and running an extra edit.
    private static GameObject RecolouredCopy(string source, string dest, Color color, float visualScale, System.Action<GameObject> extra)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(dest) == null && !AssetDatabase.CopyAsset(source, dest))
            Debug.LogError($"[SpellSetup] Couldn't copy {source} to {dest}");

        var root = PrefabUtility.LoadPrefabContents(dest);
        try
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(source);
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                // Recolour from the source's colour every run, so re-running doesn't compound.
                var srcPs = FindSame(src, root, ps);
                Color c = srcPs != null ? srcPs.main.startColor.color : ps.main.startColor.color;
                float v = Mathf.Max(c.r, c.g, c.b);
                var main = ps.main;
                main.startColor = new Color(color.r * v, color.g * v, color.b * v, c.a);
                if (ps.colorOverLifetime.enabled)
                {
                    var col = ps.colorOverLifetime;
                    col.color = Tint(col.color, color);
                }
            }
            foreach (var l in root.GetComponentsInChildren<Light>(true)) l.color = color;

            var visual = root.transform.Find("Visual");
            Transform scaled = visual != null ? visual : (root.transform.childCount > 0 ? root.transform : null);
            if (scaled != null)
            {
                var srcScaled = visual != null ? src.transform.Find("Visual") : src.transform;
                scaled.localScale = (srcScaled != null ? srcScaled.localScale : Vector3.one) * visualScale;
                foreach (var ps in scaled.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }
            }

            extra?.Invoke(root);
            PrefabUtility.SaveAsPrefabAsset(root, dest);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return AssetDatabase.LoadAssetAtPath<GameObject>(dest);
    }

    private static ParticleSystem FindSame(GameObject srcRoot, GameObject root, ParticleSystem ps)
    {
        if (srcRoot == null) return null;
        string path = AnimationUtility.CalculateTransformPath(ps.transform, root.transform);
        var t = path.Length == 0 ? srcRoot.transform : srcRoot.transform.Find(path);
        return t != null ? t.GetComponent<ParticleSystem>() : null;
    }

    private static ParticleSystem.MinMaxGradient Tint(ParticleSystem.MinMaxGradient g, Color color)
    {
        Gradient Shift(Gradient src)
        {
            if (src == null) return null;
            var keys = src.colorKeys;
            for (int i = 0; i < keys.Length; i++)
            {
                float v = Mathf.Max(keys[i].color.r, keys[i].color.g, keys[i].color.b);
                keys[i].color = new Color(color.r * v, color.g * v, color.b * v);
            }
            var outG = new Gradient { mode = src.mode };
            outG.SetKeys(keys, src.alphaKeys);
            return outG;
        }
        switch (g.mode)
        {
            case ParticleSystemGradientMode.Gradient: return new ParticleSystem.MinMaxGradient(Shift(g.gradient));
            case ParticleSystemGradientMode.TwoGradients: return new ParticleSystem.MinMaxGradient(Shift(g.gradientMin), Shift(g.gradientMax));
            default: return g;
        }
    }

    // A bright unlit body + trail so the shot reads clearly (the fireball's particles alone are too faint once recoloured).
    private static void AddCore(GameObject root, string name, Color color, Vector3 scale, PrimitiveType shape, float trailWidth)
    {
        var old = root.transform.Find("Core");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var core = GameObject.CreatePrimitive(shape);
        core.name = "Core";
        Object.DestroyImmediate(core.GetComponent<Collider>());
        core.transform.SetParent(root.transform, false);
        if (shape == PrimitiveType.Capsule) core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // lie along the flight path
        core.transform.localScale = shape == PrimitiveType.Capsule ? new Vector3(scale.x, scale.z * 0.5f, scale.y) : scale;
        var mr = core.GetComponent<MeshRenderer>();
        mr.sharedMaterial = Mat(Materials + name + "_Core.mat", "Unlit/Color", Color.Lerp(color, Color.white, 0.45f));
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = core.AddComponent<TrailRenderer>();
        trail.time = 0.18f;
        trail.widthMultiplier = trailWidth;
        trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        trail.sharedMaterial = Mat(Materials + name + "_Trail.mat", "Sprites/Default", Color.white);
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
                  new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = g;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private static Material Mat(string path, string shader, Color color)
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

    private static T GetOrCreate<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void Dirty(Object o) => EditorUtility.SetDirty(o);
}
