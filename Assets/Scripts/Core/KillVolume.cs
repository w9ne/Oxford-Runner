using UnityEngine;

/// <summary>Trigger that respawns the player at their last checkpoint (pits, out-of-bounds).</summary>
[RequireComponent(typeof(Collider))]
public class KillVolume : MonoBehaviour
{
    private void Reset() => GetComponent<Collider>().isTrigger = true;

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<FirstPersonController>();
        if (player != null) Checkpoints.Respawn(player);
    }
}
