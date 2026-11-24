using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Player Body
/// Builds the PlayerBody animator controller and gives the Player prefab its full-body model
/// (Standout7 "LOFRE Player 01", low poly) with a spellbook floating above the right hand.
/// Safe to re-run: it replaces the previous body.
/// </summary>
public static class PlayerBodySetup
{
    private const string KI = "Assets/Kevin Iglesias/Human Animations/";
    private const string ModelPrefab = "Assets/Standout7/LOFRE_Player_01/Prefabs/Player_01.prefab";
    private const string ModelMaterial = "Assets/Standout7/LOFRE_Player_01/Materials/M_Lowpo.mat";
    private const string ModelTexture = "Assets/Standout7/LOFRE_Player_01/Texture/T_Lowpo.png";
    private const string PlayerPrefab = "Assets/Prefabs/Player.prefab";
    private const string BookPrefab = "Assets/Blue Olive Studio/Magic Spellbook & Book Stand \u2013 Wizard Props/Built-In/Magic Book-Opened 1.prefab";
    private const string RightArmMask = KI + "Models/Avatar Masks/Arms/Human Arm Right Mask.mask";
    private const string Run = KI + "Animations/Male/Movement/Run/HumanM@Run01_";
    private const string IdleClip = KI + "Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string BookPose = KI + "Animations/Masked Poses/HumanM@ObjectBook01_R.fbx";

    private const string ControllerPath = "Assets/Animations/PlayerBody.controller";
    private const string BodyName = "PlayerBody";

    private static readonly Vector3 BookLocalScale = Vector3.one * 0.2f;
    private const float BodyScale = 1.3f;   // Player_01 is short (~1.45 m); scale so the shoulders sit under the camera

    // Spells spawn in front of the left palm, which thrusts out here (camera space).
    private static readonly Vector3 CastPointLocalPos = new Vector3(-0.24f, -0.14f, 0.6f);

    [MenuItem("Oxford Runner/Setup Player Body")]
    public static void Setup()
    {
        FixModelMaterial();
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefab);
        try
        {
            if (SetupOn(root)) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("[PlayerBodySetup] Player prefab now uses Player_01 with a floating spellbook.");
    }

    /// <summary>The pack ships a URP material (pink in the built-in pipeline): switch it to Standard.</summary>
    private static void FixModelMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(ModelMaterial);
        if (mat == null) return;
        if (mat.shader == null || mat.shader.name != "Standard") mat.shader = Shader.Find("Standard");
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(ModelTexture);
        mat.color = Color.white;
        mat.SetFloat("_Glossiness", 0.15f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
    }

    private static bool SetupOn(GameObject root)
    {
        var player = root.GetComponent<FirstPersonController>();
        if (player == null) { Debug.LogError("[PlayerBodySetup] Player prefab has no FirstPersonController."); return false; }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = BuildController();

        var old = player.transform.Find(BodyName);
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.transform);
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        body.name = BodyName;
        body.transform.localPosition = new Vector3(0f, -1.05f, -0.15f);
        body.transform.localRotation = Quaternion.identity;
        body.transform.localScale = Vector3.one * BodyScale;

        foreach (var mb in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        foreach (var col in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
        // Head, hair, brows, scarf and shoulder guards only cast shadows: they sit right under the
        // camera and would fill the view; your shadow still shows them.
        foreach (var smr in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (smr.name == "Head" || smr.name == "Hair" || smr.name == "Eyebrow" || smr.name == "Scarf" || smr.name == "Shoulder Guard")
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

        var animator = body.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[PlayerBodySetup] Model has no humanoid Animator; aborting.");
            Object.DestroyImmediate(body);
            return false;
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Book floating above the right hand (PlayerBody moves it every frame).
        var bookAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefab);
        if (bookAsset == null)
        {
            Debug.LogError($"[PlayerBodySetup] Book prefab not found at {BookPrefab}");
            return false;
        }
        var book = (GameObject)PrefabUtility.InstantiatePrefab(bookAsset, body.transform);
        book.name = "Spellbook";
        book.transform.localScale = BookLocalScale;
        foreach (var col in book.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        // Spells fire from the left hand: reuse (or create) a CastPoint under the camera.
        var camera = player.GetComponentInChildren<Camera>();
        Transform castPoint = camera != null ? camera.transform.Find("CastPoint") : null;
        if (castPoint == null && camera != null)
        {
            castPoint = new GameObject("CastPoint").transform;
            castPoint.SetParent(camera.transform, false);
        }
        if (castPoint != null)
        {
            castPoint.localPosition = CastPointLocalPos;
            castPoint.localRotation = Quaternion.identity;
        }

        var bodyComp = body.AddComponent<PlayerBody>();
        var caster = player.GetComponent<SpellCaster>();

        var so = new SerializedObject(bodyComp);
        so.FindProperty("controller").objectReferenceValue = player;
        so.FindProperty("cameraTransform").objectReferenceValue = camera != null ? camera.transform : null;
        so.FindProperty("spellCaster").objectReferenceValue = caster;
        so.FindProperty("book").objectReferenceValue = book.transform;
        so.FindProperty("castPoint").objectReferenceValue = castPoint;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (caster != null)
        {
            var cso = new SerializedObject(caster);
            cso.FindProperty("castPoint").objectReferenceValue = castPoint;
            cso.ApplyModifiedProperties();
        }

        // The book is only ~0.4m from the eye; the default near plane (0.3) would clip it.
        if (camera != null) camera.nearClipPlane = 0.03f;
        return true;
    }

    [MenuItem("Oxford Runner/Rebuild Player Animator")]
    public static void RebuildAnimator()
    {
        var controller = BuildController();
        foreach (var body in Object.FindObjectsByType<PlayerBody>(FindObjectsSortMode.None))
            body.GetComponent<Animator>().runtimeAnimatorController = controller;
        Debug.Log("[PlayerBodySetup] Rebuilt " + ControllerPath);
    }

    private static AnimatorController BuildController()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Animations")) AssetDatabase.CreateFolder("Assets", "Animations");
        AssetDatabase.DeleteAsset(ControllerPath);

        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        ctrl.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        ctrl.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        ctrl.AddParameter(new AnimatorControllerParameter { name = "RunSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

        // --- Base layer: 8-way locomotion ---
        var layers = ctrl.layers;
        layers[0].iKPass = true;
        ctrl.layers = layers;

        var sm = ctrl.layers[0].stateMachine;
        var locoState = ctrl.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
        tree.blendType = BlendTreeType.FreeformDirectional2D;
        tree.blendParameter = "MoveX";
        tree.blendParameterY = "MoveY";
        tree.useAutomaticThresholds = false;

        const float d = 0.7071f;
        tree.AddChild(Clip(IdleClip), Vector2.zero);
        tree.AddChild(Clip(Run + "Forward.fbx"), new Vector2(0, 1));
        tree.AddChild(Clip(Run + "Backward.fbx"), new Vector2(0, -1));
        tree.AddChild(Clip(Run + "Left.fbx"), new Vector2(-1, 0));
        tree.AddChild(Clip(Run + "Right.fbx"), new Vector2(1, 0));
        tree.AddChild(Clip(Run + "ForwardLeft.fbx"), new Vector2(-d, d));
        tree.AddChild(Clip(Run + "ForwardRight.fbx"), new Vector2(d, d));
        tree.AddChild(Clip(Run + "BackwardLeft.fbx"), new Vector2(-d, -d));
        tree.AddChild(Clip(Run + "BackwardRight.fbx"), new Vector2(d, -d));

        locoState.speedParameterActive = true;
        locoState.speedParameter = "RunSpeed";
        sm.defaultState = locoState;

        // --- Book layer: right arm holds the book (override, right-arm mask, IK for camera pinning) ---
        var bookSm = new AnimatorStateMachine { name = "Book", hideFlags = HideFlags.HideInHierarchy };
        AssetDatabase.AddObjectToAsset(bookSm, ctrl);
        var holdState = bookSm.AddState("HoldBook");
        holdState.motion = Clip(BookPose);
        bookSm.defaultState = holdState;

        ctrl.AddLayer(new AnimatorControllerLayer
        {
            name = "Book",
            stateMachine = bookSm,
            avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>(RightArmMask),
            blendingMode = AnimatorLayerBlendingMode.Override,
            defaultWeight = 1f,
            iKPass = true
        });

        AssetDatabase.SaveAssets();
        return ctrl;
    }

    private static AnimationClip Clip(string fbxPath)
    {
        var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        if (clip == null) Debug.LogError($"[PlayerBodySetup] No clip found in {fbxPath}");
        return clip;
    }
}
