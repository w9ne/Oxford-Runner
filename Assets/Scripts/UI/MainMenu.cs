using UnityEngine;

/// <summary>
/// Title screen: PLAY (tutorial, then the sandbox arena), TUTORIAL (tutorial only, back to menu),
/// SETTINGS (mouse sensitivity), EXIT. Built at runtime; the scene only needs a camera and this component.
/// </summary>
public class MainMenu : MonoBehaviour
{
    private RectTransform mainPanel, settingsPanel;

    private void Start()
    {
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var canvas = MenuUI.Canvas("MainMenuCanvas", 10).transform;
        MenuUI.FullScreen(canvas, "Backdrop", new Color(0.02f, 0.03f, 0.06f, 0.55f));

        mainPanel = MenuUI.Rect(canvas, "Main", Vector2.zero, new Vector2(800f, 900f));
        MenuUI.Label(mainPanel, "OXFORD RUNNER", 96, new Vector2(0f, 270f), new Vector2(1200f, 120f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Button(mainPanel, "Play", new Vector2(0f, 60f), GameFlow.Play);
        MenuUI.Button(mainPanel, "Tutorial", new Vector2(0f, -30f), GameFlow.PlayTutorialOnly);
        MenuUI.Button(mainPanel, "Settings", new Vector2(0f, -120f), () => Show(settings: true));
        MenuUI.Button(mainPanel, "Exit", new Vector2(0f, -210f), GameFlow.Quit);

        settingsPanel = MenuUI.SettingsPanel(canvas, () => Show(settings: false));
        Show(settings: false);
    }

    private void Show(bool settings)
    {
        mainPanel.gameObject.SetActive(!settings);
        settingsPanel.gameObject.SetActive(settings);
    }

    private void Update()
    {
        if (settingsPanel.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Show(settings: false);
    }
}
