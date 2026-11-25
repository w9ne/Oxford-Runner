using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent player settings (PlayerPrefs).</summary>
public static class GameSettings
{
    private const string SensitivityKey = "MouseSensitivity";
    public const float DefaultSensitivity = 2f;
    public const float MinSensitivity = 0.2f;
    public const float MaxSensitivity = 10f;

    public static float MouseSensitivity
    {
        get => PlayerPrefs.GetFloat(SensitivityKey, DefaultSensitivity);
        set { PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinSensitivity, MaxSensitivity)); PlayerPrefs.Save(); }
    }
}

/// <summary>
/// Which scenes follow which. "Play" runs the campaign: Tutorial -> Level 1 -> Level 2 -> Level 3 (library,
/// ends in the boss) -> main menu. "Tutorial" runs the tutorial alone and returns to the main menu.
/// Player health is carried from one level to the next.
/// </summary>
public static class GameFlow
{
    public const string MainMenuScene = "MainMenu";
    public const string TutorialScene = "Tutorial";
    public const string SandboxScene = "MovementTest";
    public const string Level1Scene = "Level1";
    public const string Level2Scene = "Level2";
    public const string Level3Scene = "Level3";

    /// <summary>Campaign order.</summary>
    public static readonly string[] Campaign = { TutorialScene, Level1Scene, Level2Scene, Level3Scene };

    /// <summary>Health to restore when the next level starts (-1 = start at full).</summary>
    public static float CarriedHealth { get; set; } = -1f;

    public enum Route { Campaign, TutorialOnly }
    public static Route CurrentRoute { get; private set; } = Route.Campaign;

    /// <summary>True while the pause menu is open; gameplay input ignores the mouse.</summary>
    public static bool Paused { get; set; }

    public static void Play()
    {
        CurrentRoute = Route.Campaign;
        CarriedHealth = -1f;
        Load(TutorialScene);
    }

    public static void PlayTutorialOnly()
    {
        CurrentRoute = Route.TutorialOnly;
        Load(TutorialScene);
    }

    public static void ToMainMenu() => Load(MainMenuScene);

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>The scene after the given one in the campaign (main menu after the last).</summary>
    public static string NextInCampaign(string scene)
    {
        int i = System.Array.IndexOf(Campaign, scene);
        return i >= 0 && i + 1 < Campaign.Length ? Campaign[i + 1] : MainMenuScene;
    }

    /// <summary>Where a level exit should go. Empty "requested" means "next in the campaign".</summary>
    public static string ResolveExit(string requested)
    {
        string current = SceneManager.GetActiveScene().name;
        if (current == TutorialScene && CurrentRoute == Route.TutorialOnly) return MainMenuScene;
        if (System.Array.IndexOf(Campaign, current) >= 0) return NextInCampaign(current);
        return string.IsNullOrEmpty(requested) ? MainMenuScene : requested;
    }

    /// <summary>Remember the player's health for the next level, then load it.</summary>
    public static void AdvanceTo(string scene, PlayerHealth health)
    {
        CarriedHealth = health != null ? health.Health : -1f;
        Load(scene);
    }

    private static void Load(string scene)
    {
        Paused = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }
}
