using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// DMC / ULTRAKILL style rank. Hits, kills, combos (hitting frozen / stunned enemies), airshots and parries
/// fill the meter and climb the ranks D..SSS; taking damage knocks it down, dying resets it, and doing nothing
/// for 5 s drains it. Every gain also adds to the level score (times the rank multiplier). Each level starts
/// fresh at rank D with 0 points; the best score per level is saved, but only once the level is cleared.
/// Added at runtime by LevelManager, so every campaign level has one.
/// </summary>
public class StyleMeter : MonoBehaviour
{
    public static StyleMeter Instance { get; private set; }

    /// <summary>PlayerPrefs key for a level's best score.</summary>
    public static string BestKey(string scene) => "BestScore_" + scene;
    public int Score { get; private set; }
    private string bestKey;
    private int savedBest, shownScore = -1;
    /// <summary>Best score saved before this run (the results screen shows "NEW BEST").</summary>
    public int SavedBest => savedBest;
    private bool cleared;

    public static readonly string[] Letters = { "D", "C", "B", "A", "S", "SS", "SSS" };
    public static readonly string[] Names =
        { "DECENT", "CAST MASTER", "BATTLE MAGE", "2ND RATE MAGE", "SORCERER", "SLINGING SORCERER", "SUPREME SPELLSLINGING SOVEREIGN" };
    public static readonly Color[] Colors =
    {
        new Color(0.6f, 0.75f, 1f), new Color(0.4f, 1f, 0.6f), new Color(1f, 0.9f, 0.3f), new Color(1f, 0.6f, 0.2f),
        new Color(1f, 0.25f, 0.25f), new Color(1f, 0.3f, 0.8f), new Color(0.85f, 0.5f, 1f),
    };
    private static readonly float[] Capacity = { 150f, 200f, 250f, 300f, 350f, 400f, 450f };   // meter points per rank

    // Points per event.
    private const float Hit = 10f, Kill = 50f, Combo = 35f, Airshot = 40f, ParryPts = 60f, ParryKill = 120f;
    private const float IdleDelay = 5f;      // seconds of no gains before the meter drains
    private const float HitLossFraction = 0.6f;   // of the current rank's bar, per hit taken
    private const int DeathScorePenalty = 500;
    private const float StackWindow = 1.5f;  // same feed line within this many seconds stacks "x2"

    private float meter;                     // total meter, all ranks laid end to end
    private int rank;
    private float lastGain = -99f;
    private float rankPop;                   // 1 on rank change, eases to 0

    private class Line { public string text, label; public int count, shown; public float time; public Color color; public bool rainbow; }
    private readonly List<Line> feed = new List<Line>();   // newest first, max 3

    // Kill modifiers: each one multiplies the kill's points by 1.5 (they stack).
    private const float ModifierBonus = 1.5f, MultiKillWindow = 1.2f;
    private static readonly Color KillGreen = new Color(0.35f, 1f, 0.45f), MultiYellow = new Color(1f, 0.88f, 0.2f);
    private FirstPersonController controller;
    private float lastKillTime = -99f;
    private int killStreak;
    private readonly System.Text.StringBuilder rainbow = new System.Text.StringBuilder();

    private Text letter, rankName, scoreText, bestText;
    private Image barFill;
    private readonly Text[] feedTexts = new Text[3];

    private void Awake()
    {
        Instance = this;
        bestKey = BestKey(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        savedBest = PlayerPrefs.GetInt(bestKey, 0);
        BuildUi();
    }

    private void Start()
    {
        var level = GetComponent<LevelManager>();
        if (level != null) { cleared = level.Cleared; level.LevelCleared += () => cleared = true; }
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p == null) return;
        controller = p.GetComponent<FirstPersonController>();
        var health = p.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Damaged += OnPlayerDamaged;
            health.Died += OnPlayerDied;
        }
        var parry = p.GetComponent<PlayerParry>();
        if (parry != null) parry.Parried += _ => Gain(ParryPts, "PARRY");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (cleared) SaveBest();   // quitting mid-level doesn't count
    }

    // ------------------------------------------------------------ events
    /// <summary>Called by Enemy after a player hit lands. combo/air are measured before the hit.</summary>
    public void EnemyHit(bool killed, bool reflected, bool combo, bool airborne)
    {
        if (killed) OnKill(reflected);
        else Gain(Hit, "HIT");
        if (combo) Gain(Combo, "COMBO");
        if (airborne) Gain(Airshot, "AIRSHOT");
    }

    // A kill, green. Killing while wall-running, in the air or sliding, and multikills (2 within 1.2 s: yellow;
    // 3+: rainbow) each multiply the kill's points by 1.5.
    private void OnKill(bool reflected)
    {
        killStreak = Time.time - lastKillTime <= MultiKillWindow ? killStreak + 1 : 1;
        lastKillTime = Time.time;
        bool wallRun = controller != null && controller.IsWallRunning, slide = controller != null && controller.IsSliding;
        bool air = controller != null && !controller.IsGrounded;
        float mult = KillMultiplier(wallRun, slide, air, killStreak);
        string how = wallRun ? "WALLRUN " : slide ? "SLIDE " : air ? "AIR " : "";
        Gain((Hit + (reflected ? ParryKill : Kill)) * mult, how + (reflected ? "PARRY KILL!" : "KILL"), KillGreen);
        if (killStreak >= 3) Push("MULTIKILL", MultiYellow, rainbow: true);
        else if (killStreak == 2) Push("MULTIKILL", MultiYellow);
    }

    /// <summary>Kill point multiplier: one movement bonus (wall-run, else slide, else air) and multikill, x1.5 each.</summary>
    public static float KillMultiplier(bool wallRun, bool slide, bool air, int killStreak)
    {
        float mult = wallRun || slide || air ? ModifierBonus : 1f;
        return killStreak >= 2 ? mult * ModifierBonus : mult;
    }

    private void OnPlayerDamaged(DamageInfo info)
    {
        meter = Mathf.Max(0f, meter - Capacity[rank] * HitLossFraction);
        Score = Mathf.Max(0, Score - Mathf.RoundToInt(info.amount * 5f));
        Push("- HIT TAKEN", new Color(1f, 0.35f, 0.3f));
    }

    private void OnPlayerDied()
    {
        meter = 0f;
        Score = Mathf.Max(0, Score - DeathScorePenalty);
        feed.Clear();
        Push("- DIED", new Color(1f, 0.35f, 0.3f));
    }

    private void Gain(float points, string label) => Gain(points, label, Color.white);

    private void Gain(float points, string label, Color color)
    {
        meter = Mathf.Min(meter + points, Total(Letters.Length));
        Score += Mathf.RoundToInt(points * Multiplier);
        lastGain = Time.time;
        Push("+ " + label, color);
    }

    private float Multiplier => 1f + rank * 0.35f;   // D 1x .. SSS 3.1x

    private static float Total(int ranks)
    {
        float t = 0f;
        for (int i = 0; i < ranks; i++) t += Capacity[i];
        return t;
    }

    private void Push(string text, Color color, bool rainbow = false)
    {
        if (feed.Count > 0 && feed[0].text == text && feed[0].rainbow == rainbow && Time.time - feed[0].time < StackWindow)
        {
            feed[0].count++;
            feed[0].time = Time.time;
        }
        else
        {
            feed.Insert(0, new Line { text = text, count = 1, time = Time.time, color = color, rainbow = rainbow });
            if (feed.Count > feedTexts.Length) feed.RemoveAt(feed.Count - 1);
        }
    }

    private void SaveBest()
    {
        if (Score > savedBest)
        {
            PlayerPrefs.SetInt(bestKey, Score);
            PlayerPrefs.Save();
        }
    }

    // ------------------------------------------------------------ update + HUD
    private void Update()
    {
        float idle = Time.time - lastGain;
        if (idle > IdleDelay) meter = Mathf.Max(0f, meter - (6f + rank * 4f) * Mathf.Min(1f, (idle - IdleDelay) / 2f) * Time.deltaTime);

        int newRank = 0;
        while (newRank < Letters.Length - 1 && meter >= Total(newRank + 1)) newRank++;
        if (newRank != rank) { rank = newRank; rankPop = 1f; }
        rankPop = Mathf.MoveTowards(rankPop, 0f, Time.deltaTime * 3f);

        Color c = Colors[rank];
        letter.text = Letters[rank];
        letter.color = c;
        letter.rectTransform.localScale = Vector3.one * (1f + rankPop * 0.6f);
        letter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f + rankPop * 14f);
        rankName.text = Names[rank];
        rankName.color = c;
        float fill = Mathf.Clamp01((meter - Total(rank)) / Capacity[rank]);
        barFill.rectTransform.anchorMax = new Vector2(fill, 1f);
        barFill.color = c;

        if (Score != shownScore)   // strings only when it changes
        {
            shownScore = Score;
            scoreText.text = Score.ToString("N0");
            bestText.text = "BEST " + Mathf.Max(Score, savedBest).ToString("N0");
        }

        for (int i = 0; i < feedTexts.Length; i++)
        {
            if (i >= feed.Count) { feedTexts[i].text = ""; continue; }
            var l = feed[i];
            if (l.shown != l.count) { l.shown = l.count; l.label = l.count > 1 ? l.text + "  x" + l.count : l.text; }   // build once per change
            float age = Time.time - l.time;
            float alpha = Mathf.Clamp01(3f - age) * (i == 0 ? 1f : 0.7f);   // fade out after ~2 s
            if (l.rainbow) { feedTexts[i].text = Rainbow(l.label, alpha); feedTexts[i].color = Color.white; continue; }
            feedTexts[i].text = l.label;
            var col = l.color;
            col.a = alpha;
            feedTexts[i].color = col;
        }
    }

    // Each letter its own colour, the hues sliding along over time (rich text; only while a rainbow line shows).
    private string Rainbow(string text, float alpha)
    {
        rainbow.Clear();
        byte a = (byte)(alpha * 255f);
        for (int i = 0; i < text.Length; i++)
        {
            Color32 c = Color.HSVToRGB(Mathf.Repeat(Time.time * 0.8f + i * 0.08f, 1f), 0.85f, 1f);
            rainbow.Append("<color=#").Append(c.r.ToString("X2")).Append(c.g.ToString("X2")).Append(c.b.ToString("X2")).Append(a.ToString("X2")).Append('>')
                   .Append(text[i]).Append("</color>");
        }
        return rainbow.ToString();
    }

    private void BuildUi()
    {
        var canvas = MenuUI.Canvas("StyleHUD", 7).transform;
        canvas.SetParent(transform, false);
        var root = MenuUI.Rect(canvas, "Style", Vector2.zero, new Vector2(420f, 360f));
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(30f, -20f);

        letter = MenuUI.Label(root, "D", 150, Vector2.zero, new Vector2(420f, 180f), Colors[0], TextAnchor.MiddleLeft, FontStyle.Bold);
        letter.rectTransform.pivot = new Vector2(0f, 0.5f);   // pops and tilts from its left edge, so "SSS" grows rightward
        letter.rectTransform.anchoredPosition = new Vector2(-200f, 90f);
        var outline = letter.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(4f, -4f);
        letter.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(8f, -8f);

        rankName = MenuUI.Label(root, Names[0], 26, new Vector2(0f, -10f), new Vector2(420f, 34f), Colors[0], TextAnchor.MiddleLeft, FontStyle.Bold);
        rankName.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

        var bar = MenuUI.Rect(root, "Bar", new Vector2(-60f, -38f), new Vector2(300f, 8f), new Color(0f, 0f, 0f, 0.55f));
        var fill = MenuUI.Rect(bar, "Fill", Vector2.zero, Vector2.zero, Colors[0]);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        barFill = fill.GetComponent<Image>();

        for (int i = 0; i < feedTexts.Length; i++)
        {
            feedTexts[i] = MenuUI.Label(root, "", i == 0 ? 30 : 24, new Vector2(0f, -72f - i * 32f), new Vector2(420f, 30f), Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
            feedTexts[i].gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        }

        scoreText = MenuUI.Label(root, "0", 34, new Vector2(0f, -180f), new Vector2(420f, 40f), Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        scoreText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        bestText = MenuUI.Label(root, "BEST 0", 20, new Vector2(0f, -210f), new Vector2(420f, 26f), new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft);
    }
}
