using System.Collections;
using UnityEngine;

/// <summary>A giant two-leaf gate. LevelManager calls Open() when the area behind it unlocks (leaves swing inward), Close() to shut it behind you.</summary>
public class GateDoor : MonoBehaviour
{
    public Transform leftHinge, rightHinge;
    public float openAngle = 100f;
    public float duration = 2.2f;

    private bool open;
    private Quaternion leftShut, rightShut;

    private void Awake()
    {
        leftShut = leftHinge.localRotation;
        rightShut = rightHinge.localRotation;
    }

    public void Open() { if (!open) { open = true; Swing(1f); Sfx.At(Sfx.Bank.gateOpen, transform.position, 0.9f, 0f); } }

    /// <summary>Swings shut again (e.g. behind the player as they go through), even if still opening.</summary>
    public void Close() { if (open) { open = false; Swing(0f); } }

    // Always swings to an absolute pose, so a Close() mid-open reverses cleanly instead of over-rotating.
    private void Swing(float openness)
    {
        StopAllCoroutines();
        StartCoroutine(SwingTo(leftShut * Quaternion.Euler(0f, -openAngle * openness, 0f),
                               rightShut * Quaternion.Euler(0f, openAngle * openness, 0f)));
    }

    private IEnumerator SwingTo(Quaternion leftTarget, Quaternion rightTarget)
    {
        Quaternion l0 = leftHinge.localRotation, r0 = rightHinge.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            leftHinge.localRotation = Quaternion.Slerp(l0, leftTarget, k);
            rightHinge.localRotation = Quaternion.Slerp(r0, rightTarget, k);
            yield return null;
        }
        leftHinge.localRotation = leftTarget;
        rightHinge.localRotation = rightTarget;
    }
}
