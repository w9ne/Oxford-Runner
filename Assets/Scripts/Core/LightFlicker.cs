using UnityEngine;

/// <summary>Torch / candle flicker: wobbles the light's intensity with Perlin noise.</summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    public float amount = 0.25f;
    public float speed = 6f;
    private Light lit;
    private float baseIntensity, seed;

    private void Awake()
    {
        lit = GetComponent<Light>();
        baseIntensity = lit.intensity;
        seed = Random.value * 100f;
    }

    private void Update() => lit.intensity = baseIntensity * (1f + (Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f) * 2f * amount);
}
