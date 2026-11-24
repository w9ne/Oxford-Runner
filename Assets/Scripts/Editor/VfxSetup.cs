using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Spell VFX
/// Swaps every spell's look to the Hovl Studio "Magic effects pack":
///  - Projectiles keep their glowing core + light, with a looping Hovl effect added under the
///    "Visual" root (so it still fades out on impact).
///  - Impacts / hit effects / the nova blast use one-shot copies of Hovl effects
///    (the pack's prefabs all loop, so copies with looping off live in Prefabs/Spells/VFX).
///  - The parry barrier gets the blue magic shield, a successful parry a holy hit.
/// Safe to re-run. Also run automatically at the end of Setup Spells / Setup Combat Assets.
/// </summary>
public static class VfxSetup
{
    private const string Hovl = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
    private const string OutDir = "Assets/Prefabs/Spells/VFX/";
    private const string ProjDir = "Assets/Prefabs/Spells/";
    private const string SpellDir = "Assets/Spells/";
    private const string FxChild = "HovlFX";

    [MenuItem("Oxford Runner/Setup Spell VFX")]
    public static void Apply()
    {
        if (!AssetDatabase.IsValidFolder(Hovl.TrimEnd('/')))
        {
            Debug.LogWarning("[VfxSetup] Hovl Studio Magic effects pack not found; skipping.");
            return;
        }
        if (!AssetDatabase.IsValidFolder(OutDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Prefabs/Spells", "VFX");

        // --- Projectiles: looping Hovl effect riding on the bolt.
        Dress(ProjDir + "Fireball_Projectile.prefab", Hovl + "Sparks/Sparks flashing red.prefab", 1.2f);
        Dress(ProjDir + "FrostLance_Projectile.prefab", Hovl + "Sparks/Sparks flashing blue.prefab", 1f);
        Dress(ProjDir + "ArcaneMissile_Projectile.prefab", Hovl + "Sparks/Sparks flashing pink.prefab", 0.8f);
        Dress(ProjDir + "EnemyBolt_Projectile.prefab", Hovl + "Character auras/Lightning aura.prefab", 0.35f);

        // --- One-shot impacts.
        var fireHit = OneShot(Hovl + "Hits and explosions/Explosion.prefab", "Fireball_Hit", 1.6f, 1f,
                              Hovl + "Sparks/Sparks explode red.prefab", Hovl + "Sparks/Sparks explode yellow.prefab",
                              Hovl + "AoE effects/Smoke AOE explosion.prefab");
        var frostHit = OneShot(Hovl + "Hits and explosions/Snow hit.prefab", "FrostLance_Hit", 0.8f);
        var electroHit = OneShot(Hovl + "Hits and explosions/Electro hit.prefab", "Lightning_Hit", 2f, 1f,
                                 Hovl + "Sparks/Sparks explode blue.prefab");
        var starHit = OneShot(Hovl + "Hits and explosions/Star hit.prefab", "ArcaneMissile_Hit", 0.6f);
        var boltHit = OneShot(Hovl + "Sparks/Sparks explode pink.prefab", "EnemyBolt_Hit", 1f);
        var novaFx = OneShot(Hovl + "AoE effects/Red energy explosion.prefab", "Nova_Blast", 1f);
        var parryHit = OneShot(Hovl + "Hits and explosions/Holy hit.prefab", "Parry_Hit", 1f);
        var shield = OneShot(Hovl + "Magic shields/Magic shield blue.prefab", "Parry_Shield", 0.6f);

        SetImpact("Spell1_Fireball.asset", fireHit, 3.5f);
        var fireball = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(SpellDir + "Spell1_Fireball.asset");
        if (fireball != null) { fireball.impactRadius = 4f; EditorUtility.SetDirty(fireball); }   // big blast
        SetImpact("Spell2_FrostLance.asset", frostHit, 2f);
        SetImpact("Spell4_ArcaneMissiles.asset", starHit, 2f);
        SetImpact("Enemy_Bolt.asset", boltHit, 2f);

        var lightning = AssetDatabase.LoadAssetAtPath<ChainLightningSpell>(SpellDir + "Spell3_ChainLightning.asset");
        if (lightning != null)
        {
            lightning.hitVfx = electroHit;
            lightning.hitVfxScale = 1f;
            lightning.hitVfxLifetime = 2f;
            EditorUtility.SetDirty(lightning);
        }
        var nova = AssetDatabase.LoadAssetAtPath<NovaSpell>(SpellDir + "Spell5_ArcaneNova.asset");
        if (nova != null)
        {
            nova.vfx = novaFx;
            nova.vfxScale = nova.radius / 3.5f;   // shockwave ring is ~7 m across at scale 1
            nova.vfxChargeTime = 1f;              // Red energy explosion bursts 1 s in
            nova.vfxLifetime = 2.5f;
            EditorUtility.SetDirty(nova);
        }

        // --- Parry visuals on the Player prefab.
        var player = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Player.prefab");
        try
        {
            var parry = player.GetComponent<PlayerParry>();
            if (parry != null)
            {
                var so = new SerializedObject(parry);
                so.FindProperty("barrierVfx").objectReferenceValue = shield;
                so.FindProperty("parryVfx").objectReferenceValue = parryHit;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player, "Assets/Prefabs/Player.prefab");
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(player); }

        AssetDatabase.SaveAssets();
        Debug.Log("[VfxSetup] Spells now use the Hovl Studio effects.");
    }

    /// <summary>The level exit portal effect (used by LevelBuilder).</summary>
    public static GameObject PortalEffect() => AssetDatabase.LoadAssetAtPath<GameObject>(Hovl + "Portals/Portal blue.prefab");

    // Turn off the projectile's old particles and add the Hovl effect under its Visual root.
    private static void Dress(string projectilePath, string fxPath, float scale)
    {
        var fxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(fxPath);
        if (fxAsset == null || AssetDatabase.LoadAssetAtPath<GameObject>(projectilePath) == null)
        {
            Debug.LogWarning($"[VfxSetup] Missing {projectilePath} or {fxPath}");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(projectilePath);
        try
        {
            var proj = root.GetComponent<SpellProjectile>();
            var visualProp = new SerializedObject(proj).FindProperty("visual");
            Transform visual = visualProp.objectReferenceValue as Transform;
            if (visual == null) visual = root.transform;

            var old = visual.Find(FxChild);
            if (old != null) Object.DestroyImmediate(old.gameObject);

            // Keep the bolt's own glowing core (it's the parry read), the Hovl effect rides on top.
            // (An earlier version switched the old particles off; switch them back on.)
            foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
            {
                var em = ps.emission;
                em.enabled = true;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                if (r != null) r.enabled = true;
            }

            var fx = (GameObject)PrefabUtility.InstantiatePrefab(fxAsset);
            PrefabUtility.UnpackPrefabInstance(fx, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            fx.name = FxChild;
            fx.transform.SetParent(visual, false);
            fx.transform.localPosition = Vector3.zero;
            fx.transform.localRotation = Quaternion.identity;
            // Undo the Visual root's own scale so "scale" is in world terms.
            float parentScale = Mathf.Max(0.0001f, visual.lossyScale.x / Mathf.Max(0.0001f, root.transform.lossyScale.x));
            fx.transform.localScale = Vector3.one * (scale / parentScale);
            foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.playOnAwake = true;
            }
            foreach (var c in fx.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            PrefabUtility.SaveAsPrefabAsset(root, projectilePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // A copy of a Hovl effect that plays once (looping off), scaled.
    private static GameObject OneShot(string source, string name, float scale, float speed = 1f, params string[] extras)
    {
        string dest = OutDir + name + ".prefab";
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(source);
        if (src == null) { Debug.LogWarning("[VfxSetup] Missing " + source); return null; }

        var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        go.transform.position = Vector3.zero;
        go.transform.localScale = Vector3.one * scale;
        foreach (var extra in extras)
        {
            var extraSrc = AssetDatabase.LoadAssetAtPath<GameObject>(extra);
            if (extraSrc == null) { Debug.LogWarning("[VfxSetup] Missing " + extra); continue; }
            var add = (GameObject)PrefabUtility.InstantiatePrefab(extraSrc);
            PrefabUtility.UnpackPrefabInstance(add, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            add.transform.SetParent(go.transform, false);
            add.transform.localPosition = Vector3.zero;
        }
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.simulationSpeed = speed;
            main.loop = false;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var saved = PrefabUtility.SaveAsPrefabAsset(go, dest);
        Object.DestroyImmediate(go);
        return saved;
    }

    private static void SetImpact(string spellAsset, GameObject fx, float lifetime)
    {
        var spell = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(SpellDir + spellAsset);
        if (spell == null || fx == null) return;
        spell.impactVfx = fx;
        spell.impactVfxLifetime = lifetime;
        EditorUtility.SetDirty(spell);
    }
}
