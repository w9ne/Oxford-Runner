using UnityEditor;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Enemy Types
/// Builds three enemy prefabs from Enemy.prefab (graybox bodies dressed with props):
///  - Enemy_Skeleton : bone-white, sword. Melee: charges in, 0.6 s wind-up (sword raised, glow), 15 dmg chop.
///  - Enemy_Mage     : purple robe + wizard hat. Squishy (0.6x HP), keeps 12-20 m away, strafes, weak bolts.
///  - Enemy_Commander: bigger, crimson + gold, hat and cape. 2.5x HP, faster, fires ~2x as often from
///                     16-26 m, dashes sideways to dodge your spells every 5 s.
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
    private const string Hat = "Assets/Kevin Iglesias/Human Animations/Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Human_MageHat.prefab";

    [MenuItem("Oxford Runner/Setup Enemy Types")]
    public static void Setup()
    {
        if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder("Assets/Materials", "Enemies");
        var bolt = AssetDatabase.LoadAssetAtPath<ProjectileSpell>("Assets/Spells/Enemy_Bolt.asset");
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
            Body(root, Mat("Enemy_Bone", new Color(0.88f, 0.85f, 0.74f)), new Vector3(0.75f, 1f, 0.75f));
            Face(root, Mat("Enemy_SkullEyes", new Color(0.12f, 0.08f, 0.06f)));
            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0.5f, 0.05f, 0.25f);
            pivot.localRotation = Quaternion.Euler(20f, 0f, 0f);
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
            Body(root, Mat("Enemy_MageRobe", new Color(0.36f, 0.2f, 0.62f)), new Vector3(0.85f, 1f, 0.85f));
            Face(root, Mat("Enemy_MageFace", new Color(0.85f, 0.75f, 0.65f)));
            Prop(Hat, root.transform, new Vector3(0f, 0.92f, 0f), Quaternion.identity, 1.1f);
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        // --- Commander
        Make(CommanderPath, root =>
        {
            root.transform.localScale = Vector3.one * 1.6f;   // shortcut: graybox commander, swap for a real model later
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
            var crimson = Mat("Enemy_CommanderArmor", new Color(0.55f, 0.08f, 0.1f));
            Body(root, crimson, new Vector3(0.95f, 1f, 0.95f));
            Face(root, Mat("Enemy_CommanderGold", new Color(1f, 0.78f, 0.25f)));
            Prop(Hat, root.transform, new Vector3(0f, 0.92f, 0f), Quaternion.identity, 1.25f);
            var cape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cape.name = "Cape";
            Object.DestroyImmediate(cape.GetComponent<Collider>());
            cape.transform.SetParent(root.transform, false);
            cape.transform.localPosition = new Vector3(0f, -0.05f, -0.45f);
            cape.transform.localRotation = Quaternion.Euler(-6f, 0f, 0f);
            cape.transform.localScale = new Vector3(0.85f, 1.6f, 0.06f);
            cape.GetComponent<Renderer>().sharedMaterial = Mat("Enemy_CommanderCape", new Color(0.12f, 0.05f, 0.08f));
            Prop("Assets/Hovl Studio/Magic effects pack/Prefabs/Character auras/Debuff.prefab", root.transform,
                 new Vector3(0f, -1f, 0f), Quaternion.identity, 1f);   // aura so it stands out in a crowd
            so.ApplyModifiedPropertiesWithoutUndo();
        });

        AssetDatabase.SaveAssets();
        Debug.Log("[EnemySetup] Skeleton, Mage and Commander prefabs built.");
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

    private static SerializedObject Fields(GameObject root) => new SerializedObject(root.GetComponent<Enemy>());

    private static void Body(GameObject root, Material mat, Vector3 scale)
    {
        var body = root.transform.Find("Body");
        if (body == null) return;
        body.localScale = scale;
        body.GetComponent<Renderer>().sharedMaterial = mat;
    }

    private static void Face(GameObject root, Material mat)
    {
        var face = root.transform.Find("Face");
        if (face != null) face.GetComponent<Renderer>().sharedMaterial = mat;
    }

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
