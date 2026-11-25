using System.Collections;
using UnityEngine;

/// <summary>
/// A structure parked up out of reach (e.g. Level 3's spiral staircase, waiting in the floor above) that
/// lowers into place when LevelManager opens it as an area gate.
/// </summary>
public class DescendOnOpen : MonoBehaviour
{
    public float drop = 13f;
    public float duration = 5f;
    private bool opened;

    public void Open()
    {
        if (opened) return;
        opened = true;
        StartCoroutine(Lower());
    }

    private IEnumerator Lower()
    {
        Vector3 from = transform.position, to = from + Vector3.down * drop;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = to;
    }
}
