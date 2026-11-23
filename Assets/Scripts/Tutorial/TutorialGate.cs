using System.Collections;
using UnityEngine;

/// <summary>
/// A barrier that sinks into the ground once every linked target is dead or destroyed.
/// Targets can be anything implementing <see cref="IDamageable"/> (training dummies, enemies...).
/// </summary>
public class TutorialGate : MonoBehaviour
{
    [SerializeField] private GameObject[] targets;
    [SerializeField] private float openDuration = 1.2f;

    public int Total => targets != null ? targets.Length : 0;
    public int Remaining { get; private set; }
    public bool IsOpen { get; private set; }

    private void Start() => Refresh();

    private void Update()
    {
        if (!IsOpen) Refresh();
    }

    private void Refresh()
    {
        int alive = 0;
        foreach (var t in targets)
        {
            if (t == null) continue;                               // destroyed
            var d = t.GetComponent<IDamageable>();
            if (d != null && !d.IsDead) alive++;
        }
        Remaining = alive;
        if (alive == 0) Open();
    }

    private void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        StartCoroutine(Sink());
    }

    private IEnumerator Sink()
    {
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * (transform.lossyScale.y + 0.1f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / openDuration)
        {
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        gameObject.SetActive(false);
    }
}
