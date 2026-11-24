using System.Collections;
using UnityEngine;

/// <summary>A giant two-leaf gate. LevelManager calls Open() when the area behind it unlocks; the leaves swing inward.</summary>
public class GateDoor : MonoBehaviour
{
    public Transform leftHinge, rightHinge;
    public float openAngle = 100f;
    public float duration = 2.2f;

    public void Open() => StartCoroutine(Swing());

    private IEnumerator Swing()
    {
        Quaternion l0 = leftHinge.localRotation, r0 = rightHinge.localRotation;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            leftHinge.localRotation = l0 * Quaternion.Euler(0f, -openAngle * k, 0f);
            rightHinge.localRotation = r0 * Quaternion.Euler(0f, openAngle * k, 0f);
            yield return null;
        }
        leftHinge.localRotation = l0 * Quaternion.Euler(0f, -openAngle, 0f);
        rightHinge.localRotation = r0 * Quaternion.Euler(0f, openAngle, 0f);
    }
}
