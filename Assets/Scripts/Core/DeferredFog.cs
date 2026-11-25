using UnityEngine;

/// <summary>
/// On the player camera: renders with deferred shading and re-applies the scene's fog.
/// Why deferred: the levels have dozens of point lights (torches, candles). In forward rendering every mesh is
/// drawn again for each light touching it: Level 3 hit ~10,000 draw calls (34 ms a frame). Deferred draws each
/// mesh once and lights the screen: ~1,000 draw calls (5 ms). Built-in deferred drops fog on opaque objects,
/// so this puts it back from the depth buffer (Resources/DeferredFog.shader).
/// </summary>
[RequireComponent(typeof(Camera))]
public class DeferredFog : MonoBehaviour
{
    private Material material;

    private void Awake()
    {
        GetComponent<Camera>().renderingPath = RenderingPath.DeferredShading;
        var shader = Resources.Load<Shader>("DeferredFog");
        if (shader != null && shader.isSupported) material = new Material(shader);
        else Debug.LogWarning("[DeferredFog] fog shader missing or unsupported; no fog on opaque objects.");
    }

    [ImageEffectOpaque]   // after opaque geometry, before transparent effects (they fog themselves)
    private void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (material != null && RenderSettings.fog && RenderSettings.fogMode == FogMode.Exponential) Graphics.Blit(src, dst, material);
        else Graphics.Blit(src, dst);
    }

    private void OnDestroy()
    {
        if (material != null) Destroy(material);
    }
}
