using System.Collections;
using UnityEngine;

/// <summary>
/// A big torch that starts unlit and flares up when LevelManager opens it as an area gate (e.g. either side
/// of Level 3's metal door, so it's obvious the way is open). Flame objects switch on, the light ramps up
/// past full with a flash, then flickers.
/// </summary>
public class Ignite : MonoBehaviour
{
    public GameObject flame;
    public Light glow;
    public float intensity = 5f;
    private bool lit;

    private void Awake()
    {
        if (flame != null) flame.SetActive(false);
        if (glow != null) glow.enabled = false;
    }

    public void Open()
    {
        if (lit) return;
        lit = true;
        Sfx.At(Sfx.Bank.torch, transform.position, 0.6f);
        StartCoroutine(Burn());
    }

    private IEnumerator Burn()
    {
        if (flame != null) flame.SetActive(true);
        if (glow == null) yield break;
        glow.enabled = true;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 1.2f)   // flash to 2.5x, settle to full
        {
            glow.intensity = intensity * (t < 0.25f ? t / 0.25f * 2.5f : Mathf.Lerp(2.5f, 1f, (t - 0.25f) / 0.75f));
            yield return null;
        }
        float seed = Random.value * 100f;
        while (true)
        {
            glow.intensity = intensity * (1f + (Mathf.PerlinNoise(seed, Time.time * 5f) - 0.5f) * 0.5f);
            yield return null;
        }
    }
}
