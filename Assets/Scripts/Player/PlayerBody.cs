using UnityEngine;

/// <summary>
/// Full-body first-person avatar (Mirror's Edge style "body awareness").
///  - Drives an 8-way locomotion blend tree from the player's Rigidbody velocity.
///  - Right hand is held out palm-down (pinned to the camera with humanoid IK) and the open
///    spellbook floats just above it, bobbing gently.
///  - Left hand moves when a spell is cast; the motion depends on the spell's CastGesture.
///  - Chest/head follow the camera pitch via LookAt IK; neck/head are collapsed so the camera sees out.
///  - Slides use a hips-pivoted "legs forward" pose; wall-runs lean the body.
///  - A camera-clearance solver pushes the body back whenever any upper-body bone gets too close
///    to the camera, so the view never clips inside the mesh (looking down, sliding, landing...).
/// Lives on the model child of the Player (the object with the Animator).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerBody : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");

    // Bones the camera must keep its distance from *while they're on screen*, with the minimum
    // distance for each (roughly the mesh thickness around that bone plus margin). Off-screen bones
    // are ignored, so the body only moves when you'd actually see it intersecting the view.
    private static readonly (HumanBodyBones bone, float radius)[] ClearanceBones =
    {
        (HumanBodyBones.Hips, 0.3f), (HumanBodyBones.Spine, 0.32f), (HumanBodyBones.Chest, 0.36f),
        (HumanBodyBones.UpperChest, 0.4f), (HumanBodyBones.Neck, 0.45f),
        (HumanBodyBones.LeftShoulder, 0.36f), (HumanBodyBones.RightShoulder, 0.36f),
        (HumanBodyBones.LeftUpperArm, 0.22f), (HumanBodyBones.RightUpperArm, 0.22f),
    };

    [Header("References")]
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private SpellCaster spellCaster;
    [SerializeField] private Transform book;                 // floating prop, placed above the right hand
    [SerializeField] private Transform castPoint;            // where spells spawn (left hand reaches here)

    [Header("Placement")]
    [SerializeField] private Vector3 standOffset = new Vector3(0f, -1.05f, -0.15f);  // feet relative to Player
    [SerializeField] private bool hideHead = true;
    [Tooltip("How far the body slides back when looking straight down (predictive; the clearance solver handles the rest).")]
    [SerializeField] private float lookDownBackShift = 0.25f;

    [Header("Camera Clearance")]
    [Tooltip("Scales every bone's clearance radius.")]
    [SerializeField] private float clearanceScale = 1f;
    [SerializeField] private float clearanceRecoverSpeed = 0.6f;
    [SerializeField] private float maxClearancePush = 0.9f;

    [Header("Locomotion")]
    [SerializeField] private float animRunSpeed = 5.5f;      // m/s the run clips were authored at
    [SerializeField] private float blendDamping = 0.1f;
    [SerializeField] private float airPlaybackSpeed = 0.35f; // legs slow down while airborne

    [Header("Book Hand (right, IK)")]
    [Tooltip("Where the right hand sits, in camera space (x right, y up, z forward).")]
    [SerializeField] private Vector3 handOffset = new Vector3(0.2f, -0.3f, 0.46f);
    [Tooltip("Hand rotation in camera space.")]
    [SerializeField] private Vector3 handRotationOffset = new Vector3(11.7f, 358.8f, 91.8f);   // palm down, fingers forward
    [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.45f, -0.5f, 0.05f);
    [SerializeField, Range(0f, 1f)] private float handIKWeight = 1f;

    [Header("Floating Book")]
    [Tooltip("How far above the right palm the book hovers (camera up).")]
    [SerializeField] private float bookHover = 0.14f;
    [Tooltip("Extra camera-space offset for the book (x right, y up, z forward).")]
    [SerializeField] private Vector3 bookOffset = Vector3.zero;
    [Tooltip("Book rotation in camera space (pages tilted toward the view).")]
    [SerializeField] private Vector3 bookRotationOffset = new Vector3(0f, 90f, 318.7f);   // pages open toward the view
    [SerializeField] private float bookBobAmount = 0.012f;
    [SerializeField] private float bookBobSpeed = 2.2f;
    [SerializeField] private float bookFollowSharpness = 18f;

    [Header("Spell books (one per spell slot, swapped with a pop when you change spell)")]
    [SerializeField] private Transform[] spellBooks;
    [SerializeField] private GameObject[] bookSwapVfx;              // burst per slot, at the book
    [Tooltip("Rotation in camera space for the (closed) spell books: cover toward the view.")]
    [SerializeField] private Vector3 spellBookRotation = new Vector3(-60f, 0f, 0f);
    [SerializeField] private float bookPopTime = 0.4f;
    [Tooltip("Turn about the book's own cover normal (180 = flip it the other way up).")]
    [SerializeField] private float spellBookSpin = 180f;
    private float bookPopStart = -10f;
    private Vector3 bookBaseScale = Vector3.one;
    private Vector3[] spellBookScales;   // each book's real size, captured once (localScale is animated during the pop)

    [Header("Cast Hand (left, IK)")]
    [Tooltip("Left palm rotation in camera space while casting (palm forward, fingers up).")]
    [SerializeField] private Vector3 leftHandRotationOffset = new Vector3(295.2f, 319.8f, 39.7f);
    [SerializeField] private Vector3 leftElbowHintOffset = new Vector3(-0.5f, -0.45f, 0.1f);
    [SerializeField] private float palmBehindCastPoint = 0.1f;
    [SerializeField] private float castHoldTime = 0.3f;      // arm stays out this long after the last cast
    [SerializeField] private float castRaiseSpeed = 14f;
    [SerializeField] private float castLowerSpeed = 5f;
    [Tooltip("Where the left hand trails while sliding (Player space, near the hip/ground) so the arm doesn't swing into view.")]
    [SerializeField] private Vector3 leftHandSlideRest = new Vector3(-0.5f, -0.8f, -0.45f);

    [Header("Look IK")]
    [SerializeField, Range(0f, 1f)] private float lookBodyWeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float lookHeadWeight = 1f;

    [Header("Sway / Bob")]
    [SerializeField] private float swayAmount = 0.02f;
    [SerializeField] private float swaySmoothing = 8f;
    [SerializeField] private float bobAmount = 0.012f;
    [SerializeField] private float bobFrequency = 11f;

    [Header("Cast Kick")]
    [SerializeField] private float castKickDistance = 0.06f;
    [SerializeField] private float castKickRecover = 10f;

    [Header("Slide / Wall-Run Pose")]
    [Tooltip("Where the hips go while sliding, relative to the Player (capsule center).")]
    [SerializeField] private Vector3 slideHipsPosition = new Vector3(0f, -0.6f, 0.15f);
    [SerializeField] private float slideLean = 65f;          // torso leans back, legs swing forward
    [SerializeField] private float wallRunLean = 15f;
    [SerializeField] private float leanSmoothing = 10f;

    private Animator animator;
    private Camera viewCamera;
    private PlayerParry parry;
    private Rigidbody rb;
    private Transform hiddenBone;
    private Transform[] clearanceBones;
    private float[] clearanceRadii;
    private float hipsHeight = 1f;

    private Vector3 swayOffset;
    private Vector2 lastLook;
    private float bobPhase;
    private float castKick;
    private float castWeight, castHoldUntil;
    private float slideBlend, wallLean;
    private float clearancePush;
    private CastGesture gesture = CastGesture.Thrust;
    private float castStartTime = -999f;
    private Vector3 bookLocal;                 // smoothed book position in camera space
    private bool bookPlaced;
    private Transform rightHand;

    public Transform Book => book;
    public float CastWeight => castWeight;
    public Quaternion LastHandGoal { get; private set; }      // debug / tuning
    public Quaternion LastLeftHandGoal { get; private set; }  // debug / tuning

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        if (controller == null) controller = GetComponentInParent<FirstPersonController>();
        if (spellCaster == null) spellCaster = GetComponentInParent<SpellCaster>();
        parry = GetComponentInParent<PlayerParry>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        rb = controller != null ? controller.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();
        if (cameraTransform != null) viewCamera = cameraTransform.GetComponent<Camera>();

        if (animator.isHuman)
        {
            // Collapse at the neck (not the head) so no stump pokes up into view.
            hiddenBone = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (hiddenBone == null) hiddenBone = animator.GetBoneTransform(HumanBodyBones.Head);

            var bones = new System.Collections.Generic.List<Transform>();
            var radii = new System.Collections.Generic.List<float>();
            foreach (var (bone, radius) in ClearanceBones)
            {
                var t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                bones.Add(t);
                radii.Add(radius);
            }
            clearanceBones = bones.ToArray();
            clearanceRadii = radii.ToArray();

            rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) hipsHeight = hips.position.y - transform.position.y;   // world units (body may be scaled)
        }

        // The camera sits inside the mesh bounds; make sure the body never gets culled.
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;

        // The body must never block our own raycasts / ground checks.
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (cameraTransform != null) lastLook = new Vector2(cameraTransform.eulerAngles.y, cameraTransform.eulerAngles.x);
        ShowBook(spellCaster != null ? spellCaster.SelectedIndex : 0, false);
    }

    private void OnEnable()
    {
        if (spellCaster != null) { spellCaster.Cast += OnSpellCast; spellCaster.SpellSelected += OnSpellSelected; }
        if (parry != null)
        {
            parry.WindowOpened += OnParryWindow;
            parry.Parried += OnParried;
        }
    }

    private void OnDisable()
    {
        if (spellCaster != null) { spellCaster.Cast -= OnSpellCast; spellCaster.SpellSelected -= OnSpellSelected; }
        if (parry != null)
        {
            parry.WindowOpened -= OnParryWindow;
            parry.Parried -= OnParried;
        }
    }

    // Parry uses the same left-hand thrust as casting: snap the palm out for the whole window.
    private void OnParryWindow()
    {
        castHoldUntil = Time.time + parry.WindowDuration;
        castWeight = Mathf.Max(castWeight, 0.6f);
        castKick = 1f;
    }

    private void OnParried(SpellProjectile p) => castKick = 1.5f;

    // Swap to the slot's book: the old one vanishes, the new one pops up spinning with a burst.
    private void OnSpellSelected(int index) => ShowBook(index, true);

    private void ShowBook(int index, bool pop)
    {
        if (spellBooks == null || spellBooks.Length == 0) return;
        index = Mathf.Clamp(index, 0, spellBooks.Length - 1);
        if (spellBookScales == null)
        {
            spellBookScales = new Vector3[spellBooks.Length];
            for (int i = 0; i < spellBooks.Length; i++) if (spellBooks[i] != null) spellBookScales[i] = spellBooks[i].localScale;
        }
        for (int i = 0; i < spellBooks.Length; i++)
            if (spellBooks[i] != null) spellBooks[i].gameObject.SetActive(i == index);
        if (spellBooks[index] == null) return;
        if (book != null && book != spellBooks[index]) spellBooks[index].SetPositionAndRotation(book.position, book.rotation);
        book = spellBooks[index];
        bookBaseScale = spellBookScales[index];
        book.localScale = bookBaseScale;
        if (!pop) return;
        bookPopStart = Time.time;
        if (bookSwapVfx != null && index < bookSwapVfx.Length && bookSwapVfx[index] != null)
        {
            var fx = Instantiate(bookSwapVfx[index], book.position, Quaternion.identity, book.parent);
            Destroy(fx, 1.5f);
        }
    }

    private void OnSpellCast(Spell spell)
    {
        gesture = spell != null ? spell.gesture : CastGesture.Thrust;
        castStartTime = Time.time;
        castKick = gesture == CastGesture.Thrust ? 1f : 0.4f;
        castHoldUntil = Time.time + GestureHold(gesture);
    }

    private float GestureHold(CastGesture g)
    {
        switch (g)
        {
            case CastGesture.Sweep: return 0.35f;
            case CastGesture.Point: return 0.45f;
            case CastGesture.Flurry: return 0.55f;
            case CastGesture.Slam: return 0.5f;
            default: return castHoldTime;
        }
    }

    /// <summary>
    /// Per-spell left-hand motion: an offset in camera space and an extra palm rotation, as a
    /// function of the time since the cast.
    /// </summary>
    private Vector3 GestureOffset(out Quaternion extraRot)
    {
        float t = Time.time - castStartTime;
        extraRot = Quaternion.identity;
        switch (gesture)
        {
            case CastGesture.Sweep:
            {
                // Slash in from the left, rolling the palm as it goes.
                float u = Mathf.Clamp01(t / 0.2f);
                float e = 1f - (1f - u) * (1f - u);
                extraRot = Quaternion.Euler(0f, 0f, Mathf.Lerp(-70f, 15f, e));
                return new Vector3(Mathf.Lerp(-0.3f, 0.06f, e), Mathf.Lerp(0.06f, -0.03f, e), Mathf.Lerp(-0.05f, 0.03f, e));
            }
            case CastGesture.Point:
            {
                // Reach out high and far; the hand trembles with the current.
                float jx = (Mathf.PerlinNoise(t * 40f, 0f) - 0.5f) * 0.02f;
                float jy = (Mathf.PerlinNoise(0f, t * 40f) - 0.5f) * 0.02f;
                extraRot = Quaternion.Euler(-25f, 0f, 0f);
                return new Vector3(0.06f + jx, 0.09f + jy, 0.1f);
            }
            case CastGesture.Flurry:
            {
                // Quick circles, like flicking out missile after missile.
                float a = t * 30f;
                return new Vector3(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, Mathf.Abs(Mathf.Sin(a * 0.5f)) * 0.05f);
            }
            case CastGesture.Slam:
            {
                // Lift the palm up, then slam it down; palm turns to face the ground.
                float u = Mathf.Clamp01(t / 0.46f);   // slam lands at ~0.3 s, when the nova goes off
                float y = u < 0.45f ? Mathf.SmoothStep(0f, 0.2f, u / 0.45f) : Mathf.Lerp(0.2f, -0.16f, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.2f));
                extraRot = Quaternion.Euler(Mathf.Lerp(0f, 75f, Mathf.Clamp01(u * 2f)), 0f, 0f);
                return new Vector3(0.04f, y, -0.04f);
            }
            default:
                return Vector3.zero;
        }
    }

    private void Update()
    {
        if (rb == null) return;

        // --- Locomotion blend tree ---
        Vector3 localVel = transform.parent != null
            ? transform.parent.InverseTransformDirection(rb.linearVelocity)
            : rb.linearVelocity;
        Vector2 planar = new Vector2(localVel.x, localVel.z);
        float speed = planar.magnitude;
        Vector2 blend = speed > 0.2f ? planar / Mathf.Max(speed, animRunSpeed) : Vector2.zero;

        bool grounded = controller == null || controller.IsGrounded;
        bool sliding = controller != null && controller.IsSliding;
        if (sliding) blend = Vector2.zero;   // legs still while sliding

        animator.SetFloat(MoveXHash, blend.x, blendDamping, Time.deltaTime);
        animator.SetFloat(MoveYHash, blend.y, blendDamping, Time.deltaTime);
        float playback = grounded ? Mathf.Clamp(speed / animRunSpeed, 1f, 1.8f) : airPlaybackSpeed;
        animator.SetFloat(RunSpeedHash, playback);

        // --- Sway (hands lag behind mouse look) ---
        if (cameraTransform != null)
        {
            Vector2 look = new Vector2(cameraTransform.eulerAngles.y, cameraTransform.eulerAngles.x);
            Vector2 delta = new Vector2(Mathf.DeltaAngle(lastLook.x, look.x), Mathf.DeltaAngle(lastLook.y, look.y));
            lastLook = look;
            Vector3 targetSway = new Vector3(-delta.x, delta.y, 0f) * swayAmount;
            targetSway = Vector3.ClampMagnitude(targetSway, 0.08f);
            swayOffset = Vector3.Lerp(swayOffset, targetSway, swaySmoothing * Time.deltaTime);
        }

        // --- Bob while running on the ground ---
        if (grounded && !sliding && speed > 0.5f) bobPhase += Time.deltaTime * bobFrequency * Mathf.Clamp01(speed / animRunSpeed);
        else bobPhase = Mathf.MoveTowards(bobPhase, Mathf.Round(bobPhase / Mathf.PI) * Mathf.PI, Time.deltaTime * 4f);

        castKick = Mathf.MoveTowards(castKick, 0f, castKickRecover * Time.deltaTime);

        // --- Cast arm ---
        bool casting = Time.time < castHoldUntil;
        castWeight = Mathf.MoveTowards(castWeight, casting ? 1f : 0f, (casting ? castRaiseSpeed : castLowerSpeed) * Time.deltaTime);

        // --- Body pose ---
        slideBlend = Mathf.Lerp(slideBlend, sliding ? 1f : 0f, leanSmoothing * Time.deltaTime);

        float wallTarget = 0f;
        if (controller != null && controller.IsWallRunning) wallTarget = controller.WallSide * wallRunLean;
        wallLean = Mathf.Lerp(wallLean, wallTarget, leanSmoothing * Time.deltaTime);

        float lookDown = 0f;
        if (cameraTransform != null)
            lookDown = Mathf.Clamp01(Mathf.DeltaAngle(0f, cameraTransform.localEulerAngles.x) / 80f);

        // Standing: feet at standOffset. Sliding: lean back around the hips so the legs swing forward.
        Quaternion standRot = Quaternion.Euler(0f, 0f, wallLean);
        Vector3 standPos = standOffset + Vector3.back * (lookDownBackShift * lookDown);

        Quaternion slideRot = Quaternion.Euler(-slideLean, 0f, 0f);
        Vector3 slidePos = slideHipsPosition - slideRot * (Vector3.up * hipsHeight);

        transform.localRotation = Quaternion.Slerp(standRot, slideRot, slideBlend);
        transform.localPosition = Vector3.Lerp(standPos, slidePos, slideBlend) + Vector3.back * clearancePush;
    }

    // Layer 0 = locomotion (look IK + left cast hand), layer 1 = book arm (right hand IK).
    private void OnAnimatorIK(int layerIndex)
    {
        if (cameraTransform == null || !animator.isHuman) return;

        if (layerIndex == 0)
        {
            // Chest + head follow where we're looking.
            animator.SetLookAtWeight(1f, lookBodyWeight, lookHeadWeight, 0f, 0.5f);
            animator.SetLookAtPosition(cameraTransform.position + cameraTransform.forward * 10f);
        }

        ApplyCastHandIK();
        if (layerIndex == 0) return;

        // Right hand (with the book) pinned to the camera.
        Vector3 bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * bobAmount, Mathf.Abs(Mathf.Sin(bobPhase)) * -bobAmount, 0f);
        Vector3 local = handOffset + swayOffset + bob + Vector3.forward * (castKick * castKickDistance);
        Vector3 handPos = cameraTransform.TransformPoint(local);

        // Hand orientation is defined relative to the camera, so the book always faces the view.
        Quaternion handRot = cameraTransform.rotation * Quaternion.Euler(handRotationOffset);
        LastHandGoal = handRot;

        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, handIKWeight);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, handIKWeight);
        animator.SetIKPosition(AvatarIKGoal.RightHand, handPos);
        animator.SetIKRotation(AvatarIKGoal.RightHand, handRot);

        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, handIKWeight);
        animator.SetIKHintPosition(AvatarIKHint.RightElbow, cameraTransform.TransformPoint(elbowHintOffset));
    }

    // Left hand thrusts out to the cast point (spells spawn just in front of the palm).
    // While sliding it rests down by the hip instead, so the leaned-back arm stays out of view.
    private void ApplyCastHandIK()
    {
        float cast = castWeight * castWeight * (3f - 2f * castWeight);   // smoothstep for a snappier feel
        float w = Mathf.Max(cast, slideBlend);

        Vector3 castTarget = castPoint != null
            ? castPoint.position - cameraTransform.forward * palmBehindCastPoint
            : cameraTransform.TransformPoint(new Vector3(-0.22f, -0.12f, 0.36f));
        Vector3 gestureOffset = GestureOffset(out Quaternion gestureRot);
        castTarget += cameraTransform.forward * (castKick * castKickDistance)
                      + cameraTransform.TransformVector(swayOffset + gestureOffset);
        Transform space = transform.parent != null ? transform.parent : transform;
        Vector3 restTarget = space.TransformPoint(leftHandSlideRest);
        Vector3 target = Vector3.Lerp(restTarget, castTarget, w > 0f ? cast / w : 0f);

        Quaternion rot = cameraTransform.rotation * gestureRot * Quaternion.Euler(leftHandRotationOffset);
        LastLeftHandGoal = rot;

        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, cast);
        animator.SetIKPosition(AvatarIKGoal.LeftHand, target);
        animator.SetIKRotation(AvatarIKGoal.LeftHand, rot);
        animator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, w);
        animator.SetIKHintPosition(AvatarIKHint.LeftElbow, cameraTransform.TransformPoint(leftElbowHintOffset));
    }

    private void LateUpdate()
    {
        if (hideHead && hiddenBone != null) hiddenBone.localScale = Vector3.one * 0.0001f;
        UpdateClearance();
        UpdateFloatingBook();
    }

    // The book hovers above the right palm. Smoothed in camera space so it trails the hand a
    // touch (feels floaty) without lagging behind when the player moves fast.
    private void UpdateFloatingBook()
    {
        if (book == null || cameraTransform == null || rightHand == null) return;
        float bob = Mathf.Sin(Time.time * bookBobSpeed) * bookBobAmount;
        Vector3 target = cameraTransform.InverseTransformPoint(rightHand.position)
                         + Vector3.up * (bookHover + bob) + bookOffset;
        bookLocal = bookPlaced ? Vector3.Lerp(bookLocal, target, 1f - Mathf.Exp(-bookFollowSharpness * Time.deltaTime)) : target;
        bookPlaced = true;
        // Pop-in after a spell change: rises from the palm, spins once and overshoots its size.
        float pop = Mathf.Clamp01((Time.time - bookPopStart) / bookPopTime);
        float rise = (1f - pop) * (1f - pop);
        book.position = cameraTransform.TransformPoint(bookLocal + Vector3.down * (rise * 0.12f));
        float sway = Mathf.Sin(Time.time * bookBobSpeed * 0.7f) * 3f;
        bool spellBook = spellBooks != null && spellBooks.Length > 0;
        Vector3 rot = (spellBook ? spellBookRotation : bookRotationOffset) + new Vector3(0f, sway, sway * 0.5f);
        book.rotation = cameraTransform.rotation * Quaternion.Euler(0f, rise * 360f, 0f) * Quaternion.Euler(rot)
                        * Quaternion.Euler(0f, spellBook ? spellBookSpin : 0f, 0f);
        if (spellBook)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;   // ease-out-back
            float s = pop >= 1f ? 1f : 1f + c3 * Mathf.Pow(pop - 1f, 3f) + c1 * Mathf.Pow(pop - 1f, 2f);
            book.localScale = bookBaseScale * Mathf.Max(0.01f, s);
        }
    }

    /// <summary>
    /// Measures how far the posed upper body intrudes into the space in front of the camera and
    /// accumulates a backward push that is applied next frame (before animation/IK).
    /// Only bones inside (or just outside) the view frustum count. Recovers slowly once there's
    /// spare room, with a dead zone to avoid oscillation.
    /// </summary>
    private void UpdateClearance()
    {
        if (cameraTransform == null || clearanceBones == null) return;

        Vector3 cam = cameraTransform.position;
        float worst = float.NegativeInfinity;
        for (int i = 0; i < clearanceBones.Length; i++)
        {
            Vector3 p = clearanceBones[i].position;
            if (!NearView(p)) continue;
            float penetration = clearanceRadii[i] * clearanceScale - Vector3.Distance(p, cam);
            if (penetration > worst) worst = penetration;
        }
        if (float.IsNegativeInfinity(worst)) worst = -1f;   // nothing on screen: free to recover

        if (worst > 0f) clearancePush += worst;
        else if (worst < -0.05f) clearancePush -= Mathf.Min(clearanceRecoverSpeed * Time.deltaTime, -worst - 0.05f);
        clearancePush = Mathf.Clamp(clearancePush, 0f, maxClearancePush);
    }

    // True if a point is in front of the camera and within the frustum plus a margin
    // (the margin accounts for mesh around the bone poking into view before the bone itself).
    private bool NearView(Vector3 worldPoint)
    {
        if (viewCamera == null) return true;
        Vector3 vp = viewCamera.WorldToViewportPoint(worldPoint);
        const float margin = 0.35f;
        return vp.z > 0.01f && vp.x > -margin && vp.x < 1f + margin && vp.y > -margin && vp.y < 1f + margin;
    }
}
