using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Title screen. Behind it, a close-up of a rune wall (the tutorial's wall-run wood, blue glow bands and
/// diamonds) slides past forever, as if the view pans along an endless wall, under a dark left-side fade. Menu down the left: PLAY (asks your name for the
/// leaderboard, then runs the campaign), TUTORIAL, LEADERBOARD, SETTINGS, EXIT. Built at runtime; the scene only
/// needs a camera and this component.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("Wall texture (Materials/Levels/Textures/Wood.png), tinted like the tutorial's wall-run walls.")]
    public Texture2D wallTexture;
    private static readonly Color Gold = new Color(1f, 0.85f, 0.45f);

    private RectTransform canvasRect, mainPanel, settingsPanel, namePanel, boardPanel;
    private InputField nameField;

    private void Start()
    {
        GameFlow.Paused = false;
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var canvas = MenuUI.Canvas("MainMenuCanvas", 10).transform;
        canvasRect = (RectTransform)canvas;
        RuneWall.Build(canvas, wallTexture, new Color(0.25f, 0.6f, 0.75f) * 0.8f, vertical: false, speed: 70f);   // the tutorial wall's tint, a bit darker
        Raw(canvas, "LeftFade").texture = Gradient();

        mainPanel = MenuUI.FullScreen(canvas, "Main", Color.clear);
        mainPanel.GetComponent<Image>().raycastTarget = false;
        var title = MenuUI.Label(mainPanel, "OXFORD\nRUNNER", 130, new Vector2(-560f, 250f), new Vector2(760f, 320f), Color.white, TextAnchor.MiddleLeft, FontStyle.Bold);
        title.lineSpacing = 0.8f;
        title.gameObject.AddComponent<Outline>().effectColor = new Color(MenuUI.Accent.r, MenuUI.Accent.g, MenuUI.Accent.b, 0.35f);
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(5f, -5f);
        MenuUI.Rect(mainPanel, "Rule", new Vector2(-640f, 80f), new Vector2(600f, 3f), Gold);

        string[] names = { "Play", "Tutorial", "Leaderboard", "Settings", "Exit" };
        System.Action[] actions = { () => Show(namePanel), GameFlow.PlayTutorialOnly, OpenBoard, () => Show(settingsPanel), GameFlow.Quit };
        for (int i = 0; i < names.Length; i++)
            MenuUI.Button(mainPanel, names[i], new Vector2(-730f, -50f - i * 88f), actions[i], 380f);

        MenuUI.Label(mainPanel, $"BEST    I  {Best(GameFlow.Level1Scene)}     II  {Best(GameFlow.Level2Scene)}     III  {Best(GameFlow.Level3Scene)}",
                     22, new Vector2(-560f, -490f), new Vector2(760f, 34f), new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleLeft);

        settingsPanel = MenuUI.SettingsPanel(canvas, () => Show(mainPanel));
        namePanel = BuildNamePanel(canvas);
        Show(mainPanel);
    }

    private RectTransform BuildNamePanel(Transform canvas)
    {
        var p = MenuUI.Rect(canvas, "NamePanel", Vector2.zero, new Vector2(820f, 460f), MenuUI.PanelColor);
        MenuUI.Label(p, "WHAT IS YOUR NAME, WIZARD?", 40, new Vector2(0f, 150f), new Vector2(780f, 60f), Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
        MenuUI.Label(p, "Your runs go on the leaderboard under this name.", 22, new Vector2(0f, 100f), new Vector2(780f, 34f), new Color(1f, 1f, 1f, 0.6f));

        var box = MenuUI.Rect(p, "NameField", new Vector2(0f, 20f), new Vector2(560f, 76f), new Color(0f, 0f, 0f, 0.6f));
        nameField = box.gameObject.AddComponent<InputField>();
        var text = MenuUI.Label(box, "", 36, Vector2.zero, new Vector2(520f, 70f), Color.white);
        text.horizontalOverflow = HorizontalWrapMode.Wrap;   // InputField needs a clipping text
        var hint = MenuUI.Label(box, "Enter a name", 36, Vector2.zero, new Vector2(520f, 70f), new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, FontStyle.Italic);
        nameField.textComponent = text;
        nameField.placeholder = hint;
        nameField.characterLimit = 16;
        nameField.text = GameSettings.PlayerName;
        nameField.onSubmit.AddListener(_ => Begin());

        MenuUI.Button(p, "Begin", new Vector2(-150f, -120f), Begin, 260f);
        MenuUI.Button(p, "Back", new Vector2(150f, -120f), () => Show(mainPanel), 260f);
        return p;
    }

    private void Begin()
    {
        GameSettings.PlayerName = nameField.text;   // blank becomes "Wizard"
        GameFlow.Play();
    }

    private void OpenBoard()
    {
        if (boardPanel != null) Destroy(boardPanel.gameObject);   // rebuilt so it's always current
        boardPanel = Leaderboard.Panel(canvasRect, -1, () => Show(mainPanel));
        Show(boardPanel);
    }

    private void Show(RectTransform panel)
    {
        foreach (var p in new[] { mainPanel, settingsPanel, namePanel, boardPanel })
            if (p != null) p.gameObject.SetActive(p == panel);
        if (panel == namePanel) nameField.ActivateInputField();
    }

    private void Update()
    {
        if (!mainPanel.gameObject.activeSelf && Input.GetKeyDown(KeyCode.Escape)) Show(mainPanel);
    }

    // Full-screen RawImage (FullScreen makes an Image; one Graphic per object, so swap it).
    private static RawImage Raw(Transform canvas, string name)
    {
        var go = MenuUI.FullScreen(canvas, name, Color.white).gameObject;
        DestroyImmediate(go.GetComponent<Image>());
        var raw = go.AddComponent<RawImage>();
        raw.raycastTarget = false;
        return raw;
    }

    // Dark on the left (behind the menu), clear by the middle of the screen.
    private static Texture2D Gradient()
    {
        var t = new Texture2D(256, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int x = 0; x < 256; x++)
        {
            float a = Mathf.Clamp01(1f - x / 150f);
            t.SetPixel(x, 0, new Color(0.01f, 0.015f, 0.04f, 0.92f * a * a + 0.15f));
        }
        t.Apply();
        return t;
    }

    private static string Best(string scene) => PlayerPrefs.GetInt(StyleMeter.BestKey(scene), 0).ToString("N0");
}
