using UnityEngine;

/// <summary>
/// Plays a particle effect's opening (its charge-up) faster, then drops back to normal speed, so an
/// effect's burst can be lined up with a spell's cast time. Optionally follows a target until then.
/// </summary>
public class ParticleTimeShift : MonoBehaviour
{
    public float chargeSpeed = 1f;
    public float chargeDuration = 0.3f;
    public Transform follow;
    public Vector3 followOffset;

    private ParticleSystem[] systems;
    private float endTime;

    private void Start()
    {
        systems = GetComponentsInChildren<ParticleSystem>(true);
        SetSpeed(chargeSpeed);
        endTime = Time.time + chargeDuration;
    }

    private void LateUpdate()
    {
        if (systems == null) return;
        if (Time.time >= endTime)
        {
            SetSpeed(1f);
            systems = null;
            enabled = false;
            return;
        }
        if (follow != null) transform.position = follow.position + followOffset;
    }

    private void SetSpeed(float s)
    {
        foreach (var ps in systems) { var m = ps.main; m.simulationSpeed = s; }
    }
}
