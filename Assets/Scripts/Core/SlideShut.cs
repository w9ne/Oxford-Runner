using System.Collections;
using UnityEngine;

/// <summary>
/// Slides by <see cref="slide"/> once, then hides <see cref="hideAfter"/>. LevelManager calls Close() for
/// closeOnEnter (Level 3's hatch halves shutting over the stair hole) and Open() for area gates (Level 3's
/// spiral staircase lowering from the floor above). Same one-way move either way.
/// </summary>
public class SlideShut : MonoBehaviour
{
    public Vector3 slide;
    public float duration = 1.2f;
    public GameObject hideAfter;
    private bool closed;

    public void Open() => Close();

    public void Close()
    {
        if (closed) return;
        closed = true;
        StartCoroutine(Move());
    }

    private IEnumerator Move()
    {
        Vector3 from = transform.position, to = from + slide;
        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            transform.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = to;
        if (hideAfter != null) hideAfter.SetActive(false);
    }
}
