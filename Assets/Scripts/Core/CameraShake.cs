using UnityEngine;

/// <summary>
/// Camera shake for big impacts (Arcane Nova). FirstPersonController adds Offset() to the camera rotation each frame.
/// </summary>
public static class CameraShake
{
    private static float strength, until, duration = 1f;

    /// <summary>Shake by up to <paramref name="degrees"/>, fading out over <paramref name="seconds"/>.</summary>
    public static void Shake(float degrees, float seconds)
    {
        if (strength * Mathf.Clamp01((until - Time.time) / duration) > degrees) return;   // a bigger shake is still running
        strength = degrees;
        duration = Mathf.Max(0.01f, seconds);
        until = Time.time + seconds;
    }

    /// <summary>Euler offset for this frame (zero when not shaking).</summary>
    public static Vector3 Offset()
    {
        float k = Mathf.Clamp01((until - Time.time) / duration);
        if (k <= 0f) return Vector3.zero;
        float s = strength * k * k, t = Time.time * 38f;
        return new Vector3((Mathf.PerlinNoise(t, 0.3f) - 0.5f) * 2f * s, (Mathf.PerlinNoise(0.7f, t) - 0.5f) * 2f * s, (Mathf.PerlinNoise(t, t) - 0.5f) * s);
    }
}
