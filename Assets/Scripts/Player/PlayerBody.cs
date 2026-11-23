using UnityEngine;

/// <summary>
/// Full-body first-person avatar (Mirror's Edge style "body awareness").
///  - Drives an 8-way locomotion blend tree from the player's Rigidbody velocity.
///  - Right hand holds an open spellbook, pinned to the camera with humanoid IK so it's always in view.
///  - Left hand thrusts out toward the cast point whenever a spell is cast.
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
    [SerializeField] private Transform book;                 // held prop, parented to the right hand
    [SerializeField] private Transform castPoint;            // where spells spawn (left hand reaches here)

    [Header("Placement")]
    [SerializeField] private Vector3 standOffset = new Vector3(0f, -1f, -0.05f);  // feet relative to Player
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
    [SerializeField] private Vector3 handOffset = new Vector3(0.3f, -0.16f, 0.3f);
    [Tooltip("Hand rotation in camera space.")]
    [SerializeField] private Vector3 handRotationOffset = new Vector3(63.6f, 188.2f, 353.1f);
    [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.45f, -0.5f, 0.05f);
    [SerializeField, Range(0f, 1f)] private float handIKWeight = 1f;

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

            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips != null) hipsHeight = transform.InverseTransformPoint(hips.position).y;
        }

        // The camera sits inside the mesh bounds; make sure the body never gets culled.
        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
            smr.updateWhenOffscreen = true;

        // The body must never block our own raycasts / ground checks.
        foreach (var col in GetComponentsInChildren<Collider>())
            col.enabled = false;

        if (cameraTransform != null) lastLook = new Vector2(cameraTransform.eulerAngles.y, cameraTransform.eulerAngles.x);
    }

    private void OnEnable()
    {
        if (spellCaster != null) spellCaster.Cast += OnSpellCast;
    }

    private void OnDisable()
    {
        if (spellCaster != null) spellCaster.Cast -= OnSpellCast;
    }

    private void OnSpellCast(Spell spell)
    {
        castKick = 1f;
        castHoldUntil = Time.time + castHoldTime;
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
        castTarget += cameraTransform.forward * (castKick * castKickDistance) + cameraTransform.TransformVector(swayOffset);
        Transform space = transform.parent != null ? transform.parent : transform;
        Vector3 restTarget = space.TransformPoint(leftHandSlideRest);
        Vector3 target = Vector3.Lerp(restTarget, castTarget, w > 0f ? cast / w : 0f);

        Quaternion rot = cameraTransform.rotation * Quaternion.Euler(leftHandRotationOffset);
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
