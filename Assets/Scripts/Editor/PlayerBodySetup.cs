using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu: Oxford Runner > Setup Player Body
/// Builds the PlayerBody animator controller and attaches a full-body model holding a spellbook
/// to the "Player" in the open scene. Safe to re-run: it replaces the previous body.
/// </summary>
public static class PlayerBodySetup
{
    private const string KI = "Assets/Kevin Iglesias/Human Animations/";
    private const string ModelPrefab = KI + "Unity Demo Scenes/Human Spellcasting Animations/Prefabs/HumanM_Dummy_Purple.prefab";
    private const string BookPrefab = KI + "Unity Demo Scenes/Human Spellcasting Animations/Prefabs/Human_MageBook.prefab";
    private const string RightArmMask = KI + "Models/Avatar Masks/Arms/Human Arm Right Mask.mask";
    private const string Run = KI + "Animations/Male/Movement/Run/HumanM@Run01_";
    private const string IdleClip = KI + "Animations/Male/Idles/HumanM@Idle01.fbx";
    private const string BookPose = KI + "Animations/Masked Poses/HumanM@ObjectBook01_R.fbx";

    private const string ControllerPath = "Assets/Animations/PlayerBody.controller";
    private const string BodyName = "PlayerBody";

    // Book placement on the right hand bone (taken from the asset's demo scene).
    private static readonly Vector3 BookLocalPos = new Vector3(0.078f, 0.023f, 0.045f);
    private static readonly Quaternion BookLocalRot = new Quaternion(-0.6875358f, -0.72385466f, -0.03946614f, -0.042088166f);

    [MenuItem("Oxford Runner/Setup Player Body")]
    public static void Setup()
    {
        var player = Object.FindFirstObjectByType<FirstPersonController>();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Setup Player Body", "No FirstPersonController found in the open scene.", "OK");
            return;
        }

        AnimatorController controller = BuildController();

        // Replace any previous body.
        var old = player.transform.Find(BodyName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPrefab);
        var body = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, player.gameObject.scene);
        PrefabUtility.UnpackPrefabInstance(body, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        Undo.RegisterCreatedObjectUndo(body, "Setup Player Body");
        body.name = BodyName;
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, -1f, -0.05f);
        body.transform.localRotation = Quaternion.identity;

        // Strip any demo scripts / colliders that came with the model.
        foreach (var mb in body.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
        foreach (var col in body.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        var animator = body.GetComponent<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogError("[PlayerBodySetup] Model has no humanoid Animator; aborting.");
            Object.DestroyImmediate(body);
            return;
        }
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // Book in the right hand.
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var bookAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefab);
        var book = (GameObject)PrefabUtility.InstantiatePrefab(bookAsset, player.gameObject.scene);
        book.name = "Spellbook";
        book.transform.SetParent(hand, false);
        book.transform.localPosition = BookLocalPos;
        book.transform.localRotation = BookLocalRot;
        foreach (var col in book.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);

        // Spells fire out of the book.
        var bookCastPoint = new GameObject("BookCastPoint").transform;
        bookCastPoint.SetParent(book.transform, false);

        var bodyComp = body.AddComponent<PlayerBody>();
        var camera = player.GetComponentInChildren<Camera>();
        var caster = player.GetComponent<SpellCaster>();

        var so = new SerializedObject(bodyComp);
        so.FindProperty("controller").objectReferenceValue = player;
        so.FindProperty("cameraTransform").objectReferenceValue = camera != null ? camera.transform : null;
        so.FindProperty("spellCaster").objectReferenceValue = caster;
        so.FindProperty("book").objectReferenceValue = book.transform;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (caster != null)
        {
            var cso = new SerializedObject(caster);
            cso.FindProperty("castPoint").objectReferenceValue = bookCastPoint;
            cso.ApplyModifiedProperties();
        }

        // The book is only ~0.4m from the eye; the default near plane (0.3) would clip it.
        if (camera != null)
        {
            Undo.RecordObject(camera, "Camera near clip");
            camera.nearClipPlane = 0.03f;
        }

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Selection.activeGameObject = body;
        Debug.Log("[PlayerBodySetup] Player body + spellbook set up. Tweak 'Hand Offset' / 'Hand Rotation Offset' on PlayerBody in Play mode.");
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
