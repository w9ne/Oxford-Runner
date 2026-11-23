using UnityEngine;

/// <summary>
/// Tracks the player's last checkpoint for the current scene and respawns them there.
/// Static so any trigger (tutorial steps, level checkpoints, kill volumes) can use it without wiring.
/// </summary>
public static class Checkpoints
{
    public static Vector3 Position { get; private set; }
    public static float Yaw { get; private set; }
    public static bool HasCheckpoint { get; private set; }

    /// <summary>Raised after the player is respawned (UI flashes, counters, etc).</summary>
    public static event System.Action Respawned;

    public static void Set(Vector3 position, float yaw)
    {
        Position = position;
        Yaw = yaw;
        HasCheckpoint = true;
    }

    public static void Clear() => HasCheckpoint = false;

    public static void Respawn(FirstPersonController player)
    {
        if (player == null) return;
        if (!HasCheckpoint) Set(player.transform.position, player.transform.eulerAngles.y);
        player.Teleport(Position, Yaw);
        Respawned?.Invoke();
    }

    // Domain reload may be disabled in the editor; make sure stale checkpoints don't leak between plays.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HasCheckpoint = false;
        Respawned = null;
    }
}
