using UnityEngine;

/// <summary>
/// Full-body first-person avatar (Mirror's Edge style "body awareness").
///  - Drives an 8-way locomotion blend tree from the player's Rigidbody velocity.
///  - Uses humanoid IK to pull the right hand (holding the spellbook) to a point fixed
///    relative to the camera, so the book is always visible in the lower-right of the view.
///  - Chest/head follow the camera pitch via LookAt IK.
///  - Hides the head bone so the camera doesn't see the inside of the face.
///  - Leans the body for slides and wall-runs, and "punches" the book forward on cast.
/// Lives on the model child of the Player (the object with the Animator).
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerBody : MonoBehaviour
{
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int RunSpeedHash = Animator.StringToHash("RunSpeed");

    [Header("References")]
    [SerializeField] private FirstPersonController controller;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private SpellCaster spellCaster;
    [SerializeField] private Transform book;                 // held prop, parented to the right hand

    [Header("Placement")]
    [SerializeField] private Vector3 standOffset = new Vector3(0f, -1f, -0.05f);  // feet relative to Player
    [SerializeField] private bool hideHead = true;
    [Tooltip("How far the body slides back when looking straight down, so the shoulders don't block the view.")]
    [SerializeField] private float lookDownBackShift = 0.25f;

    [Header("Locomotion")]
    [SerializeField] private float animRunSpeed = 5.5f;      // m/s the run clips were authored at
    [SerializeField] private float blendDamping = 0.1f;
    [SerializeField] private float airPlaybackSpeed = 0.35f; // legs slow down while airborne

    [Header("Book Hand (IK)")]
    [Tooltip("Where the right hand sits, in camera space (x right, y up, z forward).")]
    [SerializeField] private Vector3 handOffset = new Vector3(0.3f, -0.16f, 0.3f);
    [Tooltip("Hand rotation in camera space. Tweak until the book faces you nicely.")]
    [SerializeField] private Vector3 handRotationOffset = new Vector3(63.6f, 188.2f, 353.1f);
    [Tooltip("Elbow hint, in camera space.")]
    [SerializeField] private Vector3 elbowHintOffset = new Vector3(0.45f, -0.5f, 0.05f);
    [SerializeField, Range(0f, 1f)] private float handIKWeight = 1f;

    [Header("Look IK")]
    [SerializeField, Range(0f, 1f)] private float lookBodyWeight = 0.15f;
    [SerializeField, Range(0f, 1f)] private float lookHeadWeight = 1f;

    [Header("Sway / Bob")]
    [SerializeField] private float swayAmount = 0.02f;
    [SerializeField] private float swaySmoothing = 8f;
    [SerializeField] private float bobAmount = 0.012f;
    [SerializeField] private float bobFrequency = 11f;

    [Header("Cast Kick")]
    [SerializeField] private float castKickDistance = 0.12f;
    [SerializeField] private float castKickRecover = 10f;

    [Header("Leans")]
    [SerializeField] private float slideLean = 60f;          // degrees leaned back while sliding
    [SerializeField] private float slideForwardShift = 1.1f; // keeps the chest under the lowered camera
    [SerializeField] private float wallRunLean = 15f;
    [SerializeField] private float leanSmoothing = 10f;

    private Animator animator;
    private Rigidbody rb;
    private Transform headBone;

    private Vector3 swayOffset;
    private Vector2 lastLook;
    private float bobPhase;
    private float castKick;
    private float slideBlend, wallLean;

    public Transform Book => book;
    public Quaternion LastHandGoal { get; private set; }   // debug / tuning

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.applyRootMotion = false;

        if (controller == null) controller = GetComponentInParent<FirstPersonController>();
        if (spellCaster == null) spellCaster = GetComponentInParent<SpellCaster>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        rb = controller != null ? controller.GetComponent<Rigidbody>() : GetComponentInParent<Rigidbody>();

        headBone = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;

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

    private void OnSpellCast(Spell spell) => castKick = 1f;

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

        // --- Sway (book lags behind mouse look) ---
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

        // --- Leans ---
        float slideTarget = sliding ? 1f : 0f;
        slideBlend = Mathf.Lerp(slideBlend, slideTarget, leanSmoothing * Time.deltaTime);

        float wallTarget = 0f;
        if (controller != null && controller.IsWallRunning) wallTarget = controller.WallSide * wallRunLean;
        wallLean = Mathf.Lerp(wallLean, wallTarget, leanSmoothing * Time.deltaTime);

        float lookDown = 0f;
        if (cameraTransform != null)
            lookDown = Mathf.Clamp01(Mathf.DeltaAngle(0f, cameraTransform.localEulerAngles.x) / 80f);

        transform.localPosition = standOffset
            + Vector3.forward * (slideForwardShift * slideBlend)
            + Vector3.back * (lookDownBackShift * lookDown * (1f - slideBlend));
        transform.localRotation = Quaternion.Euler(-slideLean * slideBlend, 0f, wallLean);
    }

    // Layer 0 = locomotion (look IK), layer 1 = book arm (hand IK). Both have IK Pass enabled.
    private void OnAnimatorIK(int layerIndex)
    {
        if (cameraTransform == null || !animator.isHuman) return;

        if (layerIndex == 0)
        {
            // Chest + head follow where we're looking.
            animator.SetLookAtWeight(1f, lookBodyWeight, lookHeadWeight, 0f, 0.5f);
            animator.SetLookAtPosition(cameraTransform.position + cameraTransform.forward * 10f);
            return;
        }

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

    private void LateUpdate()
    {
        // Collapse the head so the camera (which sits where the head is) sees out cleanly.
        if (hideHead && headBone != null) headBone.localScale = Vector3.one * 0.0001f;
    }
}
