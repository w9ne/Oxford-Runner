using UnityEngine;

/// <summary>
/// Floating enemies (kinematic, so they hang in the air): a slow up-and-down bob, and with wander &gt; 0 they
/// glide between random points within that radius of where they started, only picking points with a clear path.
/// </summary>
public class Hover : MonoBehaviour
{
    public float amplitude = 0.4f;
    public float period = 2.6f;
    public float wander;           // metres; 0 = stay put
    public float speed = 5f;
    private Vector3 home, anchor, target, vel;
    private float phase, retargetAt;
    private Rigidbody rb;

    private void Start()
    {
        home = anchor = target = transform.position;
        phase = Random.value * period;
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (GameFlow.PlayerDead) return;   // freeze with everyone else
        if (wander > 0f)
        {
            if ((anchor - target).sqrMagnitude < 1f || Time.time > retargetAt) PickTarget();
            anchor = Vector3.SmoothDamp(anchor, target, ref vel, 0.6f, speed, Time.fixedDeltaTime);   // eases in and out, no sharp turns
        }
        Vector3 p = anchor + Vector3.up * (Mathf.Sin((Time.time + phase) / period * Mathf.PI * 2f) * amplitude);
        if (rb != null && rb.isKinematic) rb.MovePosition(p); else transform.position = p;
    }

    // A few tries for a spot whose path is clear (the cast starts inside our own collider, so it ignores it).
    private void PickTarget()
    {
        retargetAt = Time.time + 4f;
        for (int i = 0; i < 6; i++)
        {
            Vector2 r = Random.insideUnitCircle * wander;
            Vector3 c = home + new Vector3(r.x, Random.Range(-1f, 1f), r.y), d = c - anchor;
            if (!Physics.SphereCast(anchor, 0.8f, d.normalized, out _, d.magnitude + 0.8f, ~0, QueryTriggerInteraction.Ignore)) { target = c; return; }
        }
        target = home;   // boxed in: drift back to the start
    }
}
