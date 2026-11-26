using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Enemy Types
/// Builds the enemy prefabs from Enemy.prefab, all on Monster Pack's rigged models (2.6 m, a head taller than you)
/// with Kevin Iglesias' run blend and a cast on each wind-up (EnemyBody.controller); the body glows as the telegraph:
///  - Enemy (base)   : tower / garrison caster, the skeleton mage.
///  - Enemy_Skeleton : skeleton, sword in the right hand. Melee: charges in, 0.6 s wind-up, 15 dmg chop.
///  - Enemy_Mage     : skeleton mage. Squishy (0.4x HP), keeps 12-20 m away, strafes, weak bolts.
///  - Enemy_Commander: orc with a dark aura, 1.3x bigger (~3.4 m). 2.5x HP, faster, fires ~2x as often
///                     from 16-26 m, dashes sideways to dodge your spells every 5 s.
/// LevelManager mixes them (skeletons first, then mages, 1-2 commanders per area). Safe to re-run.
/// </summary>
public static class EnemySetup
{
    private const string Base = "Assets/Prefabs/Enemy.prefab";
    public const string SkeletonPath = "Assets/Prefabs/Enemy_Skeleton.prefab";
    public const string MagePath = "Assets/Prefabs/Enemy_Mage.prefab";
    public const string CommanderPath = "Assets/Prefabs/Enemy_Commander.prefab";
    private const string MatDir = "Assets/Materials/Enemies/";
    private const string Sword = "Assets/Standout7/LOFRE_Player_01/Prefabs/Sword_01.prefab";
    // Monster Pack's own rigs, exported from its Blender source (Blender_source.zip) and imported as Humanoid
    // (rigify names mapped by hand in the .meta), so Kevin Iglesias' human clips drive them.
    private const string Rigs = "Assets/Monster Pack/Rigged/";
    private const string MonsterMaterial = "Assets/Monster Pack/Prefabs/Skeleton.prefab";   // its shared palette material
    private const string KI = "Assets/Kevin Iglesias/Human Animations/Animations/Male/";
    private const string ControllerPath = "Assets/Animations/EnemyBody.controller";
    private const float ModelHeight = 2.6f;   // a head taller than the player

    [MenuItem("Oxford Runner/Setup Enemy Types")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Materials", "Enemies");
        var bolt = AssetDatabase.LoadAssetAtPath<ProjectileSpell>("Assets/Spells/Enemy_Bolt.asset");
        // The base enemy is also the tower / garrison caster: it gets the mage model too.
        var baseRoot = PrefabUtility.LoadPrefabContents(Base);
        var controller = BuildController();
        try { MageLook(baseRoot, controller); PrefabUtility.SaveAsPrefabAsset(baseRoot, Base); }
        finally { PrefabUtility.UnloadPrefabContents(baseRoot); }
        var mageBolt = MageBolt(bolt);

        // --- Skeleton
        Make(SkeletonPath, root =>
        {
            var so = Fields(root);
            so.FindProperty("healthScale").floatValue = 0.5f;
            so.FindProperty("speedScale").floatValue = 1.6f;
            so.FindProperty("intervalScale").floatValue = 1f;
            so.FindProperty("melee").boolValue = true;
            so.FindProperty("meleeRange").floatValue = 2.2f;
            so.FindProperty("meleeWindup").floatValue = 0.6f;
            so.FindProperty("meleeDamage").floatValue = 15f;
            so.FindProperty("meleeCooldown").floatValue = 1.2f;
            so.FindProperty("attackSpell").objectReferenceValue = null;
            so.FindProperty("telegraphColor").colorValue = new Color(1f, 0.45f, 0.1f);
            so.FindProperty("turnSpeed").floatValue = 10f;
            var anim = Rigged(root, Rigs + "Skeleton_Rigged.fbx", controller);
            var hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(hand != null ? hand : root.transform, false);   // the sword moves with the hand
            pivot.rotation = root.transform.rotation * Quaternion.Euler(20f, 0f, 0f);
            pivot.localScale = Vector3.one / pivot.parent.lossyScale.x;
            Prop(Sword, pivot, Vector3.zero, Quaternion.identity, 1f);
            so.FindProperty("weapon").objectReferenceValue = pivot;
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // --- Mage
        Make(MagePath, root =>
        {
            var so = Fields(root);
            so.FindProperty("healthScale").floatValue = 0.4f;
            so.FindProperty("speedScale").floatValue = 1f;
            so.FindProperty("intervalScale").floatValue = 1f;
            so.FindProperty("keepMin").floatValue = 12f;
            so.FindProperty("keepMax").floatValue = 20f;
            so.FindProperty("attackSpell").objectReferenceValue = mageBolt;
            so.FindProperty("telegraphColor").colorValue = new Color(1f, 0.2f, 0.9f);
            MageLook(root, controller);
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // --- Commander
        Make(CommanderPath, root =>
        {
            root.transform.localScale = Vector3.one * 1.3f;   // ~3.4 m: the big caster
            var so = Fields(root);
            so.FindProperty("healthScale").floatValue = 2.5f;
            so.FindProperty("speedScale").floatValue = 1.5f;
            so.FindProperty("intervalScale").floatValue = 0.55f;   // fires about twice as often
            so.FindProperty("keepMin").floatValue = 16f;
            so.FindProperty("keepMax").floatValue = 26f;
            so.FindProperty("attackRange").floatValue = 42f;
            so.FindProperty("attackSpell").objectReferenceValue = bolt;
            so.FindProperty("canDash").boolValue = true;
            so.FindProperty("dashCooldown").floatValue = 5f;
            so.FindProperty("dashSpeed").floatValue = 16f;
            so.FindProperty("dashDuration").floatValue = 0.25f;
            so.FindProperty("telegraphColor").colorValue = new Color(1f, 0.15f, 0.1f);
            Rigged(root, Rigs + "Orc_Rigged.fbx", controller);   // a big spell-casting orc
            Prop("Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Debuff.prefab", root.transform,
                 new Vector3(0f, -1f, 0f), Quaternion.identity, 1f);   // aura so it stands out in a crowd
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        AssetDatabase.SaveAssets();
        Debug.Log("[EnemySetup] Skeleton, Mage and Commander prefabs built.");
    }

    public const string BossPath = "Assets/Prefabs/Enemy_Boss.prefab";
    private const string Fx = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
    private const string PlayerModel = "Assets/Standout7/LOFRE_Player_01/Prefabs/Player_01.prefab";

    /// <summary>The final boss: the player's hero model all in black on the base enemy, driven by Boss. 200 HP, lock-on 4x slower, its body stays for the death blasts.</summary>
    [MenuItem("Oxford Runner/Setup Boss")]
    public static void SetupBoss()
    {
        if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Materials", "Enemies");
        Make(BossPath, root =>
        {
            var so = Fields(root);
            so.FindProperty("maxHealth").floatValue = 350f;
            so.FindProperty("homingDamageScale").floatValue = 0.35f;   // a parried missile volley was a one-shot
            so.FindProperty("healthScale").floatValue = 1f;
            so.FindProperty("attackSpell").objectReferenceValue = null;
            so.FindProperty("canDash").boolValue = false;
            so.FindProperty("brainControlled").boolValue = true;
            so.FindProperty("destroyOnDeath").boolValue = false;
            so.FindProperty("lockTimeScale").floatValue = 4f;
            so.FindProperty("lockHoldTime").floatValue = 1.5f;   // a lock on it breaks after 1.5 s
            so.FindProperty("frozenDamageMultiplier").floatValue = 1.5f;
            so.FindProperty("telegraphColor").colorValue = new Color(1f, 0.1f, 0.1f);
            so.ApplyModifiedPropertiesWithoutUndo();
            // The player's hero model, all black, in place of the capsule (named Body so Boss squashes it to slide).
            foreach (var n in new[] { "Body", "Face" }) { var t = root.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
            var cap = root.GetComponent<CapsuleCollider>();   // the base enemy is taller now; the boss is player-sized
            cap.height = 2f;
            cap.center = Vector3.zero;
            so.FindProperty("telegraphRenderer").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel), root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Body";
            model.transform.localPosition = new Vector3(0f, -1f, 0f);   // feet at the bottom of the capsule
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * 1.3f;            // same size as the player's body
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            var shadow = Mat("Enemy_Shadow", new Color(0.02f, 0.02f, 0.025f));
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.sharedMaterials = Enumerable.Repeat(shadow, r.sharedMaterials.Length).ToArray();
            var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/PlayerBody.controller");
            anim.applyRootMotion = false;
            var glow = new GameObject("ShadowGlow").AddComponent<Light>();   // so the black body reads against the dark arena
            glow.transform.SetParent(root.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.3f, 0.6f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.15f, 0.25f);
            glow.range = 6f;
            glow.intensity = 3f;
            glow.renderMode = LightRenderMode.ForcePixel;
            Prop(Fx + "Character auras/Debuff.prefab", root.transform, new Vector3(0f, -1f, 0f), Quaternion.identity, 1f);   // dark aura
            var boss = root.GetComponent<Boss>() ?? root.AddComponent<Boss>();
            boss.fireball = AssetDatabase.LoadAssetAtPath<ProjectileSpell>("Assets/Spells/Spell1_Fireball.asset");
            boss.missiles = AssetDatabase.LoadAssetAtPath<ProjectileSpell>("Assets/Spells/Spell4_ArcaneMissiles.asset");
            boss.blast = AssetDatabase.LoadAssetAtPath<NovaSpell>("Assets/Spells/Spell5_ArcaneNova.asset");
            boss.chargeCircleVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Magic circles/Magic circle.prefab");
            boss.releaseVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "AoE effects/Red energy explosion.prefab");
            boss.parryShieldVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "Magic shields/Magic shield pink.prefab");
            boss.smallBlastVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "AoE effects/Smoke AOE explosion.prefab");
            boss.bigBlastVfx = AssetDatabase.LoadAssetAtPath<GameObject>(Fx + "AoE effects/Ground AOE explosion.prefab");
        });
        AssetDatabase.SaveAssets();
    }

    public static Enemy Load(string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        return go != null ? go.GetComponent<Enemy>() : null;
    }

    // Dress a fresh copy of the base enemy and save it over the old prefab (same GUID, so scene references survive).
    private static void Make(string path, System.Action<GameObject> dress)
    {
        var root = PrefabUtility.LoadPrefabContents(Base);
        try
        {
            dress(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void MageLook(GameObject root, AnimatorController controller) => Rigged(root, Rigs + "SkellMage_Rigged.fbx", controller);

    /// <summary>
    /// Swaps the capsule Body for a rigged Monster Pack model (EnemyBody controller: runs by velocity, casts on each
    /// wind-up), ModelHeight tall with its feet at the bottom of a matching capsule. The whole body is the attack
    /// telegraph (Enemy picks the skinned mesh when there's no Face); the Face block goes.
    /// </summary>
    private static Animator Rigged(GameObject root, string modelPath, AnimatorController controller)
    {
        foreach (var n in new[] { "Body", "Face" }) { var t = root.transform.Find(n); if (t != null) Object.DestroyImmediate(t.gameObject); }
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        float height = src.GetComponentInChildren<SkinnedMeshRenderer>().bounds.max.y;   // feet at the model's origin
        var model = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        model.name = "Body";
        model.transform.localPosition = new Vector3(0f, -1f, 0f);       // feet at the capsule's bottom (pivot 1 m below centre)
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * (ModelHeight / height);
        var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
        smr.sharedMaterial = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterMaterial).GetComponent<Renderer>().sharedMaterial;
        smr.updateWhenOffscreen = false;
        var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
        anim.runtimeAnimatorController = controller;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // off-screen enemies skip the bone work
        var cap = root.GetComponent<CapsuleCollider>();
        cap.height = ModelHeight;
        cap.center = new Vector3(0f, (ModelHeight - 2f) * 0.5f, 0f);  // grows upward, bottom stays put
        var so = Fields(root);
        so.FindProperty("telegraphRenderer").objectReferenceValue = null;   // Enemy falls back to the model's skinned mesh
        so.ApplyModifiedPropertiesWithoutUndo();
        return anim;
    }

    /// <summary>EnemyBody.controller: the player's 8-way run blend (MoveX / MoveY / RunSpeed) and an Attack trigger
    /// that plays a one-hand cast, then returns.</summary>
    private static AnimatorController BuildController()
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
        ctrl.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        var sm = ctrl.layers[0].stateMachine;
        var loco = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.useAutomaticThresholds = false;
        const float d = 0.7071f;
        string run = KI + "Movement/Run/HumanM@Run01_";
        tree.AddChild(Clip(KI + "Idles/HumanM@Idle01.fbx"), Vector2.zero);
        foreach (var (n, v) in new[] { ("Forward", new Vector2(0, 1)), ("Backward", new Vector2(0, -1)), ("Left", new Vector2(-1, 0)), ("Right", new Vector2(1, 0)),
                                        ("ForwardLeft", new Vector2(-d, d)), ("ForwardRight", new Vector2(d, d)), ("BackwardLeft", new Vector2(-d, -d)), ("BackwardRight", new Vector2(d, -d)) })
            tree.AddChild(Clip(run + n + ".fbx"), v);
        loco.speedParameterActive = true;
        loco.speedParameter = "RunSpeed";
        sm.defaultState = loco;
        var attack = sm.AddState("Attack");
        attack.motion = Clip(KI + "Combat/Spellcasting/MagicAttacks/Directional/HumanM@MagicAttackDirect1H01_R.fbx");
        var toAttack = sm.AddAnyStateTransition(attack);
        toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
        toAttack.duration = 0.1f;
        toAttack.canTransitionToSelf = false;
        var back = attack.AddTransition(loco);
        back.hasExitTime = true;
        back.exitTime = 0.9f;
        back.duration = 0.2f;
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    private static AnimationClip Clip(string fbxPath)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogError("[EnemySetup] No clip in " + fbxPath);
        return clip;
    }

    private static SerializedObject Fields(GameObject root) => new SerializedObject(root.GetComponent<Enemy>());

    private static void Prop(string path, Transform parent, Vector3 localPos, Quaternion localRot, float scale)
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (src == null) { Debug.LogWarning("[EnemySetup] Missing " + path); return; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = Vector3.one * scale;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    private static Material Mat(string name, Color color)
    {
        string path = MatDir + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.color = color;
        mat.SetFloat("_Glossiness", 0.2f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // Weaker copy of the enemy bolt for mages ("low level" spell).
    private static ProjectileSpell MageBolt(ProjectileSpell bolt)
    {
        const string path = "Assets/Spells/Enemy_MageBolt.asset";
        if (bolt == null) return null;
        var mage = AssetDatabase.LoadAssetAtPath<ProjectileSpell>(path);
        if (mage == null)
        {
            mage = Object.Instantiate(bolt);
            AssetDatabase.CreateAsset(mage, path);
        }
        EditorUtility.CopySerialized(bolt, mage);
        mage.name = "Enemy_MageBolt";
        mage.displayName = "Mage Bolt";
        mage.damage = 6f;
        mage.impactRadius = 0.9f;
        EditorUtility.SetDirty(mage);
        return mage;
    }
}
