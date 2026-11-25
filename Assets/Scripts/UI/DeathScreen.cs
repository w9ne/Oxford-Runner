using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player death in a campaign level: everything stops (GameFlow.PlayerDead freezes enemies, enemy shots in the air
/// fizzle, the player's controls switch off), the camera jolts and shakes, then falls backwards to the floor looking
/// up at the ceiling, and "YOU DIED" fades in with RESTART LEVEL and QUIT TO MENU.
/// </summary>
public class DeathScreen : MonoBehaviour
{
    private const float ShakeTime = 0.45f, FallTime = 1.1f, SettleTime = 0.2f;
    private static readonly Vector3 FloorEyeOffset = new Vector3(0f, -0.75f, -0.45f);   // camera local position on the floor
    private static readonly Vector3 FloorEyeAngles = new Vector3(-78f, 0f, 16f);         // looking up, head tipped

    private Transform cam;
    private CanvasGroup ui;

    public static void Play(PlayerHealth health)
    {
        if (GameFlow.PlayerDead) return;
        GameFlow.PlayerDead = true;
        new GameObject("DeathScreen").AddComponent<DeathScreen>().Begin(health);
    }

    private void Begin(PlayerHealth health)
    {
        var player = health.gameObject;
        foreach (var b in player.GetComponents<MonoBehaviour>())
            if (b is FirstPersonController || b is SpellCaster || b is PlayerParry || b is MissileLockOn) b.enabled = false;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null) { rb.linearVelocity = Vector3.zero; rb.isKinematic = true; }
        foreach (var body in player.GetComponentsInChildren<PlayerBody>()) body.gameObject.SetActive(false);   // arms / book out of the shot

        var shots = new System.Collections.Generic.List<SpellProjectile>(SpellProjectile.Active);
        foreach (var p in shots) if (p != null) p.Fizzle();   // yours too: they could kill the last frozen enemy and open the portal

        var camera = player.GetComponentInChildren<Camera>();
        cam = camera != null ? camera.transform : null;
        BuildUi();
        StartCoroutine(Sequence());
    }

    private IEnumerator Sequence()
    {
        if (cam != null)
        {
            Vector3 p0 = cam.localPosition;
            Quaternion r0 = cam.localRotation;
            // Sudden jolt + shake.
            for (float t = 0f; t < ShakeTime; t += Time.unscaledDeltaTime)
            {
                float k = 1f - t / ShakeTime;
                cam.localPosition = p0 + Random.insideUnitSphere * (0.09f * k) + Vector3.down * (0.12f * (1f - k));
                cam.localRotation = r0 * Quaternion.Euler(Random.Range(-4f, 4f) * k, Random.Range(-4f, 4f) * k, Random.Range(-7f, 7f) * k);
                yield return null;
            }
            // Fall backwards (speeding up like a real fall), then a small settle.
            Vector3 p1 = cam.localPosition;
            Quaternion r1 = cam.localRotation, floor = Quaternion.Euler(FloorEyeAngles);
            for (float t = 0f; t < FallTime; t += Time.unscaledDeltaTime)
            {
                float k = t / FallTime; k *= k;
                cam.localPosition = Vector3.Lerp(p1, FloorEyeOffset, k);
                cam.localRotation = Quaternion.Slerp(r1, floor, k);
                yield return null;
            }
            for (float t = 0f; t < SettleTime; t += Time.unscaledDeltaTime)   // thud
            {
                float bounce = Mathf.Sin(t / SettleTime * Mathf.PI) * 0.06f;
                cam.localPosition = FloorEyeOffset + Vector3.up * bounce;
                yield return null;
            }
            cam.localPosition = FloorEyeOffset;
            cam.localRotation = floor;
        }

        yield return new WaitForSecondsRealtime(0.4f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        ui.gameObject.SetActive(true);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f) { ui.alpha = t; yield return null; }
        ui.alpha = 1f;
        ui.interactable = true;
    }

    private void BuildUi()
    {
        var canvas = MenuUI.Canvas("DeathCanvas", 70).transform;
        canvas.SetParent(transform, false);
        ui = canvas.gameObject.AddComponent<CanvasGroup>();
        ui.alpha = 0f;
        ui.interactable = false;
        MenuUI.FullScreen(canvas, "Dim", new Color(0.25f, 0f, 0f, 0.55f));
        var title = MenuUI.Label(canvas, "YOU DIED", 130, new Vector2(0f, 140f), new Vector2(1400f, 170f), new Color(0.9f, 0.12f, 0.1f), TextAnchor.MiddleCenter, FontStyle.Bold);
        title.gameObject.AddComponent<Outline>().effectDistance = new Vector2(5f, -5f);
        MenuUI.Button(canvas, "Restart Level", new Vector2(0f, -40f), GameFlow.RestartLevel);
        MenuUI.Button(canvas, "Quit to Menu", new Vector2(0f, -130f), GameFlow.ToMainMenu);
        canvas.gameObject.SetActive(false);
    }
}
