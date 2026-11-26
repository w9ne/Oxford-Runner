using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// End-of-level rank screen (shown by SceneExit when you enter a campaign level's portal). Time stops, the
/// level's style score counts up and a bar fills from rank D to the rank earned; the letter pops at each rank
/// it passes. Click / Space / Enter continues to the next scene (health carries over).
/// With autoClose &gt; 0 (Level 3's torch hall) play goes on underneath and the card closes itself, no scene change.
/// Rank = score as a share of the level's SSS score (<see cref="SssScore"/>).
/// </summary>
public class LevelResults : MonoBehaviour
{
    /// <summary>Score that earns SSS in each level.</summary>
    public static readonly Dictionary<string, int> SssScore = new Dictionary<string, int>
    {
        { GameFlow.Level1Scene, 6500 }, { GameFlow.Level2Scene, 13500 }, { GameFlow.Level3Scene, 18500 },
    };
    /// <summary>Share of the SSS score each rank starts at: D, C, B, A, S, SS, SSS.</summary>
    public static readonly float[] RankAt = { 0f, 0.2f, 0.35f, 0.5f, 0.65f, 0.8f, 1f };

    public static int RankFor(int score, int sss)
    {
        int r = 0;
        while (r < RankAt.Length - 1 && score >= Mathf.RoundToInt(RankAt[r + 1] * sss)) r++;   // rounded: 0.2f * 8000 is 1600.00002
        return r;
    }

    private const float FillTime = 2.6f;
    private const float BarWidth = 760f;

    private string nextScene;
    private PlayerHealth health;
    private int score, best, sss, rank = -1;
    private float started, rankPop, autoClose;
    private Text letter, rankName, scoreText, hint;
    private RectTransform fill;
    private Image fillImage;

    public static void Show(string nextScene, PlayerHealth health, float autoClose = 0f)
    {
        var go = new GameObject("LevelResults");
        var r = go.AddComponent<LevelResults>();
        r.nextScene = nextScene;
        r.health = health;
        var style = StyleMeter.Instance;
        r.score = style != null ? style.Score : 0;
        r.best = style != null ? style.SavedBest : 0;
        string scene = SceneManager.GetActiveScene().name;
        r.sss = SssScore.TryGetValue(scene, out int s) ? s : 10000;
        PlayerPrefs.SetInt(VictoryScreen.LastKey(scene), r.score);   // this run's score, for the victory screen
        var level = FindAnyObjectByType<LevelManager>();
        r.Build(level != null ? level.LevelName : scene, Time.timeSinceLevelLoad);
        r.autoClose = autoClose;
        GameFlow.ShowingResults = true;   // no pause menu over the card
        if (autoClose > 0f) return;   // keep playing

        GameFlow.Paused = true;   // no look / casting; GameFlow.Load clears both
        Time.timeScale = 0f;
    }

    private void Build(string levelName, float seconds)
    {
        var canvas = MenuUI.Canvas("LevelResultsCanvas", 60).transform;
        canvas.SetParent(transform, false);
        MenuUI.FullScreen(canvas, "Dim", new Color(0f, 0f, 0f, PortalFall.Instance != null ? 0.2f : 0.75f));   // falling: keep the rune wall visible
        var panel = MenuUI.Rect(canvas, "Panel", Vector2.zero, new Vector2(900f, 640f), MenuUI.PanelColor);

        MenuUI.Label(panel, "LEVEL CLEARED", 60, new Vector2(0f, 250f), new Vector2(860f, 80f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Label(panel, levelName.ToUpperInvariant(), 26, new Vector2(0f, 195f), new Vector2(860f, 40f), MenuUI.Accent);
        MenuUI.Label(panel, "TIME", 30, new Vector2(-200f, 130f), new Vector2(300f, 44f), Color.white, TextAnchor.MiddleLeft);
        MenuUI.Label(panel, TutorialManager.FormatTime(seconds), 30, new Vector2(200f, 130f), new Vector2(300f, 44f), Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
        MenuUI.Label(panel, "STYLE", 30, new Vector2(-200f, 80f), new Vector2(300f, 44f), Color.white, TextAnchor.MiddleLeft);
        scoreText = MenuUI.Label(panel, "0", 30, new Vector2(200f, 80f), new Vector2(300f, 44f), Color.white, TextAnchor.MiddleRight, FontStyle.Bold);

        letter = MenuUI.Label(panel, "D", 170, new Vector2(0f, -50f), new Vector2(500f, 200f), StyleMeter.Colors[0], TextAnchor.MiddleCenter, FontStyle.Bold);
        letter.gameObject.AddComponent<Outline>().effectDistance = new Vector2(4f, -4f);
        rankName = MenuUI.Label(panel, "", 26, new Vector2(0f, -150f), new Vector2(860f, 40f), StyleMeter.Colors[0], TextAnchor.MiddleCenter, FontStyle.Bold);

        // The bar, with a tick and letter where each rank starts.
        var bar = MenuUI.Rect(panel, "Bar", new Vector2(0f, -205f), new Vector2(BarWidth, 18f), new Color(1f, 1f, 1f, 0.12f));
        fill = MenuUI.Rect(bar, "Fill", Vector2.zero, Vector2.zero, StyleMeter.Colors[0]);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = new Vector2(0f, 1f);
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;
        fillImage = fill.GetComponent<Image>();
        for (int i = 0; i < RankAt.Length; i++)
        {
            float x = (RankAt[i] - 0.5f) * BarWidth;
            MenuUI.Rect(bar, "Tick", new Vector2(x, 0f), new Vector2(2f, 26f), new Color(1f, 1f, 1f, 0.5f));
            MenuUI.Label(bar, StyleMeter.Letters[i], 20, new Vector2(x, -30f), new Vector2(60f, 26f), StyleMeter.Colors[i], TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        hint = MenuUI.Label(panel, "", 22, new Vector2(0f, -285f), new Vector2(860f, 34f), new Color(1f, 1f, 1f, 0.75f));
        started = Time.unscaledTime;
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - started) / FillTime);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        int shown = Mathf.RoundToInt(score * eased);
        scoreText.text = shown.ToString("N0");

        int r = RankFor(shown, sss);
        if (r != rank) { rank = r; rankPop = 1f; }
        rankPop = Mathf.MoveTowards(rankPop, 0f, Time.unscaledDeltaTime * 3f);
        Color c = StyleMeter.Colors[rank];
        letter.text = StyleMeter.Letters[rank];
        letter.color = c;
        letter.rectTransform.localScale = Vector3.one * (1f + rankPop * 0.5f);
        rankName.text = StyleMeter.Names[rank];
        rankName.color = c;
        fill.anchorMax = new Vector2(Mathf.Clamp01((float)shown / sss), 1f);
        fillImage.color = c;

        if (t < 1f) return;
        if (autoClose > 0f)
        {
            hint.text = score > best ? "NEW BEST!" : $"BEST {best:N0}";
            if (Time.unscaledTime - started > autoClose) { GameFlow.ShowingResults = false; Destroy(gameObject); }
            return;
        }
        var fall = PortalFall.Instance;
        bool ready = fall == null || fall.Ready;
        hint.text = (score > best ? "NEW BEST!     " : $"BEST {best:N0}     ") + (ready ? "PRESS SPACE TO DROP IN" : "LOADING...");
        if (ready && Time.unscaledTime - started > FillTime + 0.5f
            && (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
        {
            enabled = false;
            if (fall != null) fall.Drop(); else GameFlow.AdvanceTo(nextScene, health);
        }
    }
}
