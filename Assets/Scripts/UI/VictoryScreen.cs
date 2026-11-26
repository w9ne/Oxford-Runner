using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Shown when the boss dies: this run's score and rank for each level (the level-end cards save them), the
/// total, and the leaderboard (this run is added under the player's name). Time stops; Main Menu leaves.
/// </summary>
public class VictoryScreen : MonoBehaviour
{
    private static readonly string[] Levels = { GameFlow.Level1Scene, GameFlow.Level2Scene, GameFlow.Level3Scene };

    public static string LastKey(string scene) => "LastScore_" + scene;

    private GameObject board;

    public static void Show()
    {
        var style = StyleMeter.Instance;
        if (style != null) PlayerPrefs.SetInt(LastKey(SceneManager.GetActiveScene().name), style.Score);   // Level 3 counts the boss too
        new GameObject("VictoryScreen").AddComponent<VictoryScreen>().Build();
        Sfx.Play(Sfx.Bank.victory, 1f, 0f);
        GameFlow.ShowingResults = true;
        GameFlow.Paused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Build()
    {
        MenuUI.EnsureEventSystem();
        var canvas = MenuUI.Canvas("VictoryCanvas", 80).transform;
        canvas.SetParent(transform, false);
        MenuUI.FullScreen(canvas, "Dim", new Color(0f, 0f, 0f, 0.8f));
        var panel = MenuUI.Rect(canvas, "Panel", Vector2.zero, new Vector2(1000f, 760f), MenuUI.PanelColor);
        MenuUI.Label(panel, "VICTORY", 90, new Vector2(0f, 300f), new Vector2(960f, 110f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Label(panel, "Your shadow is gone.", 26, new Vector2(0f, 235f), new Vector2(960f, 40f), MenuUI.Accent);

        int total = 0;
        for (int i = 0; i < Levels.Length; i++)
        {
            float y = 150f - i * 70f;
            bool played = PlayerPrefs.HasKey(LastKey(Levels[i]));
            int score = PlayerPrefs.GetInt(LastKey(Levels[i]), 0);
            total += score;
            int sss = LevelResults.SssScore.TryGetValue(Levels[i], out int s) ? s : 10000;
            int rank = LevelResults.RankFor(score, sss);
            MenuUI.Label(panel, "LEVEL " + (i + 1), 34, new Vector2(-300f, y), new Vector2(300f, 50f), Color.white, TextAnchor.MiddleLeft);
            MenuUI.Label(panel, played ? score.ToString("N0") : "-", 34, new Vector2(60f, y), new Vector2(300f, 50f), Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            MenuUI.Label(panel, played ? StyleMeter.Letters[rank] : "", 44, new Vector2(330f, y), new Vector2(120f, 60f), StyleMeter.Colors[rank], TextAnchor.MiddleCenter, FontStyle.Bold);
        }
        MenuUI.Label(panel, "TOTAL", 38, new Vector2(-300f, -70f), new Vector2(300f, 50f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleLeft, FontStyle.Bold);
        MenuUI.Label(panel, total.ToString("N0"), 38, new Vector2(60f, -70f), new Vector2(300f, 50f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleRight, FontStyle.Bold);

        int place = Leaderboard.Add(GameSettings.PlayerName, total);
        MenuUI.Button(panel, "Leaderboard", new Vector2(-220f, -250f), () => board.SetActive(true), 380f);
        MenuUI.Button(panel, "Main Menu", new Vector2(220f, -250f), GameFlow.ToMainMenu, 380f);
        if (place >= 0) MenuUI.Label(panel, $"#{place + 1} ON THE LEADERBOARD", 28, new Vector2(0f, -150f), new Vector2(900f, 40f), new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
        board = Leaderboard.Panel(canvas, place, () => board.SetActive(false)).gameObject;
        board.SetActive(false);
    }
}
