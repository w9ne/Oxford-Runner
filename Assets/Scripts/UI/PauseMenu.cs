using UnityEngine;

/// <summary>
/// Escape pauses the game: RESUME, SETTINGS (mouse sensitivity), MAIN MENU. Lives on the Player, so every
/// level gets it. Time stops and gameplay ignores the mouse while it's open.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    private GameObject root;
    private RectTransform mainPanel, settingsPanel;

    private void Start()
    {
        var canvas = MenuUI.Canvas("PauseMenu", 50).transform;
        canvas.SetParent(transform, false);
        root = canvas.gameObject;
        MenuUI.FullScreen(canvas, "Dim", new Color(0f, 0f, 0f, 0.6f));

        mainPanel = MenuUI.Rect(canvas, "Main", Vector2.zero, new Vector2(560f, 460f), MenuUI.PanelColor);
        MenuUI.Label(mainPanel, "PAUSED", 56, new Vector2(0f, 160f), new Vector2(500f, 70f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Button(mainPanel, "Resume", new Vector2(0f, 50f), Resume);
        MenuUI.Button(mainPanel, "Settings", new Vector2(0f, -40f), () => Show(true));
        MenuUI.Button(mainPanel, "Main Menu", new Vector2(0f, -130f), GameFlow.ToMainMenu);

        settingsPanel = MenuUI.SettingsPanel(canvas, () => Show(false));
        root.SetActive(false);
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.Escape) || GameFlow.ShowingResults || GameFlow.PlayerDead) return;
        if (!GameFlow.Paused) Pause();
        else if (settingsPanel.gameObject.activeSelf) Show(false);
        else Resume();
    }

    private void Show(bool settings)
    {
        mainPanel.gameObject.SetActive(!settings);
        settingsPanel.gameObject.SetActive(settings);
    }

    private void Pause()
    {
        GameFlow.Paused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Show(false);
        root.SetActive(true);
    }

    public void Resume()
    {
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        // Leaving the scene while paused (e.g. Main Menu button) must not leave the game frozen.
        if (GameFlow.Paused) { GameFlow.Paused = false; Time.timeScale = 1f; }
    }
}
