using System.Collections;
using UnityEngine;

/// <summary>
/// "Beam in" spawn effect (Star Trek style): a column of light streaks rises where the enemy will stand
/// (Resources/BeamIn = Hovl's Teleport effect) while a light swells, then the enemy flickers into view.
/// The enemy stays inactive while beaming (no AI, no collider, can't be hit or locked on) but still counts as
/// alive for LevelManager, so an area can't clear before everyone has arrived.
/// </summary>
public class BeamIn : MonoBehaviour
{
    private const float BeamTime = 0.9f, ShimmerTime = 0.4f, LightIntensity = 6f;
    private static readonly Color BeamColor = new Color(0.55f, 0.8f, 1f);
    private static GameObject prefab;
    private static bool loaded;

    /// <summary>Hides <paramref name="target"/> now and beams it in after <paramref name="delay"/> seconds.</summary>
    public static void Play(GameObject target, float delay = 0f)
    {
        if (target != null) Sfx.At(Sfx.Bank.beamIn, target.transform.position, 0.5f);
        if (target == null) return;
        target.SetActive(false);
        var go = new GameObject("BeamIn");
        go.transform.position = target.transform.position;
        var beam = go.AddComponent<BeamIn>();
        beam.StartCoroutine(beam.Run(target, delay));
    }

    private IEnumerator Run(GameObject target, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (!loaded) { prefab = Resources.Load<GameObject>("BeamIn"); loaded = true; }
        GameObject fx = prefab != null ? Instantiate(prefab, transform.position, Quaternion.identity, transform) : null;

        var light = new GameObject("BeamLight").AddComponent<Light>();
        light.transform.SetParent(transform, false);
        light.transform.localPosition = Vector3.up * 1.2f;
        light.type = LightType.Point;
        light.color = BeamColor;
        light.range = 7f;
        light.intensity = 0f;

        for (float t = 0f; t < BeamTime; t += Time.deltaTime)
        {
            light.intensity = LightIntensity * t / BeamTime;
            yield return null;
        }

        if (target != null)
        {
            target.SetActive(true);
            // Flicker into view: visible more and more often, then solid.
            var renderers = target.GetComponentsInChildren<Renderer>();
            var wasOn = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) wasOn[i] = renderers[i].enabled;
            for (float t = 0f; t < ShimmerTime; t += 0.05f)
            {
                bool on = Random.value < 0.25f + 0.75f * t / ShimmerTime;
                for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null && wasOn[i]) renderers[i].enabled = on;
                light.intensity = LightIntensity * (1f - t / ShimmerTime);
                yield return new WaitForSeconds(0.05f);
            }
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = wasOn[i];
        }

        light.enabled = false;
        if (fx != null) foreach (var ps in fx.GetComponentsInChildren<ParticleSystem>()) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        Destroy(gameObject, 1f);   // let the last streaks fade
    }
}
