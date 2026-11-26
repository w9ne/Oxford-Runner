using UnityEngine;

/// <summary>
/// Escape pauses the game: RESUME, UNSTICK SELF (back to the last checkpoint), SETTINGS (mouse sensitivity), MAIN MENU. Lives on the Player, so every
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

        mainPanel = MenuUI.Rect(canvas, "Main", Vector2.zero, new Vector2(560f, 550f), MenuUI.PanelColor);
        MenuUI.Label(mainPanel, "PAUSED", 56, new Vector2(0f, 205f), new Vector2(500f, 70f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Button(mainPanel, "Resume", new Vector2(0f, 95f), Resume);
        MenuUI.Button(mainPanel, "Unstick Self", new Vector2(0f, 5f), Unstick);
        MenuUI.Button(mainPanel, "Settings", new Vector2(0f, -85f), () => Show(true));
        MenuUI.Button(mainPanel, "Main Menu", new Vector2(0f, -175f), GameFlow.ToMainMenu);

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

    // Stuck in geometry or somewhere you can't climb out of: back to the last checkpoint (the level start if none).
    private void Unstick()
    {
        Resume();
        var player = GetComponent<FirstPersonController>();
        if (player != null) Checkpoints.Respawn(player);
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
        Sfx.Play(Sfx.Bank.pause, 0.7f, 0f);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Show(false);
        root.SetActive(true);
    }

    public void Resume()
    {
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        Sfx.Play(Sfx.Bank.unpause, 0.7f, 0f);
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
