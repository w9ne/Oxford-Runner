using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The boss arena. Stepping inside heals you to full, shuts the door behind you and sets the hallway as the
/// checkpoint; the boss bar at the top fills up, then the boss wakes. Dying in the fight puts you back in the
/// hallway (door open again) and resets the boss. Killing it shows the victory screen.
/// </summary>
public class BossFight : MonoBehaviour
{
    /// <summary>True from the moment you enter the arena until the boss dies (PlayerHealth: no death screen).</summary>
    public static bool InFight => instance != null && instance.started && !instance.won;

    public Boss boss;
    public GateDoor door;
    public float radius = 36f;
    public Transform hallwayRespawn;
    public string bossName = "YOUR SHADOW";
    public float fillTime = 2.5f;

    private static BossFight instance;
    private Transform player;
    private PlayerHealth health;
    private Enemy bossHealth;
    private bool started, awake, won;
    private float shown;
    private GameObject hud;
    private RectTransform fill, trail;

    private void Awake() => instance = this;
    private void OnDestroy() { if (instance == this) instance = null; Checkpoints.Respawned -= ResetFight; }

    private void Start()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) { player = p.transform; health = p.GetComponent<PlayerHealth>(); }
        bossHealth = boss.GetComponent<Enemy>();
        Checkpoints.Respawned += ResetFight;
    }

    private void Update()
    {
        if (player == null || won) return;
        if (!started)
        {
            Vector3 d = player.position - transform.position; d.y = 0f;
            if (d.magnitude < radius - 4f && Mathf.Abs(player.position.y - transform.position.y) < 6f) Begin();
            return;
        }
        float target = bossHealth != null ? bossHealth.Health / bossHealth.MaxHealth : 0f;
        if (!awake)
        {
            shown = Mathf.MoveTowards(shown, 1f, Time.deltaTime / fillTime);   // the bar fills before the fight
            if (shown >= 1f) { awake = true; boss.Wake(transform.position, radius); }
        }
        else shown = target;
        fill.anchorMax = new Vector2(shown, 1f);
        trail.anchorMax = new Vector2(Mathf.Max(shown, Mathf.MoveTowards(trail.anchorMax.x, shown, Time.deltaTime * 0.4f)), 1f);   // damage trail
    }

    private void Begin()
    {
        started = true;
        awake = false;
        shown = 0f;
        if (health != null) health.Heal(health.MaxHealth);
        if (door != null) door.Close();
        Sfx.PlayMusic(Sfx.Bank.bossMusic, 0.8f);
        if (hallwayRespawn != null) Checkpoints.Set(hallwayRespawn.position, hallwayRespawn.eulerAngles.y);
        if (GameSettings.PlayerName.Length > 0) bossName = GameSettings.PlayerName.ToUpperInvariant() + "'S SHADOW";
        BuildHud();
    }

    // The player died in the fight and respawned in the hallway.
    private void ResetFight()
    {
        if (!started || won) return;
        started = awake = false;
        if (hud != null) Destroy(hud);
        foreach (var p in new List<SpellProjectile>(SpellProjectile.Active)) if (p != null) p.Fizzle();
        boss.ResetFight();
        Sfx.SceneMusic();
        if (door != null) door.Open();
    }

    /// <summary>Called by the boss once its death blasts are over.</summary>
    public static void Won()
    {
        if (instance == null) return;
        instance.won = true;
        Sfx.PlayMusic(null, 2.5f);   // the music dies with it
        if (instance.hud != null) Destroy(instance.hud);
        VictoryScreen.Show();
    }

    private void BuildHud()
    {
        var canvas = MenuUI.Canvas("BossHUD", 7);
        hud = canvas.gameObject;
        var t = canvas.transform;
        var name = MenuUI.Label(t, bossName, 34, Vector2.zero, new Vector2(900f, 44f), new Color(1f, 0.85f, 0.85f), TextAnchor.MiddleCenter, FontStyle.Bold);
        Top(name.rectTransform, -90f);   // under the level objective line
        name.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        var back = MenuUI.Rect(t, "BossBar", Vector2.zero, new Vector2(900f, 22f), new Color(0f, 0f, 0f, 0.7f));
        Top(back, -125f);
        trail = Bar(back, "Trail", new Color(1f, 0.9f, 0.9f, 0.8f));
        fill = Bar(back, "Fill", new Color(0.75f, 0.08f, 0.15f));
    }

    private static RectTransform Bar(RectTransform parent, string name, Color c)
    {
        var r = MenuUI.Rect(parent, name, Vector2.zero, Vector2.zero, c);
        r.anchorMin = Vector2.zero;
        r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 0.5f);
        r.offsetMin = new Vector2(3f, 3f);
        r.offsetMax = new Vector2(0f, -3f);
        return r;
    }

    private static void Top(RectTransform rt, float y)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
    }
}
