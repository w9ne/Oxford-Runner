using UnityEngine;

/// <summary>
/// One station of the movement tutorial. Entering the trigger shows its prompt and sets a checkpoint
/// at <see cref="respawnPoint"/> (defaults to this transform, facing its forward).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class TutorialStep : MonoBehaviour
{
    public int order;
    public string title = "Step";
    [TextArea(2, 4)] public string instructions;
    public Transform respawnPoint;
    [Tooltip("Optional: a gate whose remaining targets are shown while this step is active.")]
    public TutorialGate gate;

    private void Reset() => GetComponent<BoxCollider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<FirstPersonController>() == null) return;
        if (TutorialManager.Instance != null) TutorialManager.Instance.EnterStep(this);
    }

    public Transform Spawn => respawnPoint != null ? respawnPoint : transform;
}
