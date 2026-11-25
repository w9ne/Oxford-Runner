using UnityEngine;

/// <summary>Slow up-and-down bob for floating enemies (kinematic, so they hang in the air).</summary>
public class Hover : MonoBehaviour
{
    public float amplitude = 0.4f;
    public float period = 2.6f;
    private Vector3 home;
    private float phase;
    private Rigidbody rb;

    private void Start()
    {
        home = transform.position;
        phase = Random.value * period;
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        Vector3 p = home + Vector3.up * (Mathf.Sin((Time.time + phase) / period * Mathf.PI * 2f) * amplitude);
        if (rb != null && rb.isKinematic) rb.MovePosition(p); else transform.position = p;
    }
}
