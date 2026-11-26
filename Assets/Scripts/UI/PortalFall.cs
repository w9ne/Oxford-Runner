using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Portal transition (in the spirit of ULTRAKILL's level drops). Entering a portal: the screen becomes a rune
/// bookshelf wall rushing upward, as if you're falling past it, while the next scene loads in the background and
/// the score card (LevelResults, or a short card for the tutorial) sits on top. Space / click once it's loaded:
/// the scene switches under the falling wall, then the whole wall whips down off the bottom of the screen, revealing
/// the new area: the illusion of dropping into it. Survives the scene change, then removes itself.
/// </summary>
public class PortalFall : MonoBehaviour
{
    public static PortalFall Instance { get; private set; }

    /// <summary>The next scene has finished loading and is waiting for the player.</summary>
    public bool Ready => load != null && load.progress >= 0.9f;

    private const float FallSpeed = 2600f;   // canvas px per second
    private const float SlideTime = 0.35f;    // the wall whipping down off screen
    private AsyncOperation load;
    private RectTransform slider;
    private Text prompt;
    private bool ownCard, going;

    /// <summary>Starts the fall to <paramref name="scene"/>. With a title the fall shows its own small card
    /// (tutorial); otherwise LevelResults is the card and calls <see cref="Drop"/>.</summary>
    public static void Begin(string scene, PlayerHealth health, string title = null, string body = null)
    {
        if (Instance != null) return;
        GameFlow.CarriedHealth = health != null ? health.Health : -1f;
        var go = new GameObject("PortalFall");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<PortalFall>();
        Instance.Build(title, body);
        Sfx.Play(Sfx.Bank.portal, 0.9f, 0f);
        if (title != null) { GameFlow.Paused = true; GameFlow.ShowingResults = true; Time.timeScale = 0f; }   // ShowingResults: no pause menu mid-load (a second scene load would hang)   // tutorial: the world stops under the card (LevelResults does this itself)
        Instance.load = SceneManager.LoadSceneAsync(scene);
        Instance.load.allowSceneActivation = false;
    }

    private void Build(string title, string body)
    {
        var canvas = MenuUI.Canvas("PortalFallCanvas", 55);   // under the LevelResults card (60)
        canvas.transform.SetParent(transform, false);
        slider = MenuUI.FullScreen(canvas.transform, "Slider", Color.clear);   // everything that slides away at the end
        slider.GetComponent<Image>().raycastTarget = false;
        RuneWall.Build(slider, Resources.Load<Texture2D>("PortalWall"), new Color(0.75f, 0.75f, 0.85f), vertical: true, speed: FallSpeed);
        MenuUI.FullScreen(slider, "Shade", new Color(0f, 0f, 0f, 0.25f)).GetComponent<Image>().raycastTarget = false;
        ownCard = title != null;
        if (ownCard)
        {
            var panel = MenuUI.Rect(canvas.transform, "Card", Vector2.zero, new Vector2(900f, 300f), MenuUI.PanelColor);
            MenuUI.Label(panel, title, 56, new Vector2(0f, 70f), new Vector2(860f, 70f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            MenuUI.Label(panel, body ?? "", 28, new Vector2(0f, -10f), new Vector2(860f, 40f), MenuUI.Accent);
            prompt = MenuUI.Label(panel, "", 24, new Vector2(0f, -95f), new Vector2(860f, 36f), new Color(1f, 1f, 1f, 0.75f));
        }
        StartCoroutine(SlideIn());
    }

    private void Update()
    {
        if (going || !ownCard) return;
        prompt.text = Ready ? "PRESS SPACE TO DROP IN" : "LOADING...";
        if (Ready && (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return))) Drop();
    }

    /// <summary>Switch to the loaded scene (call once Ready).</summary>
    public void Drop()
    {
        if (going || !Ready) return;
        going = true;
        foreach (var card in FindObjectsByType<LevelResults>(FindObjectsSortMode.None)) Destroy(card.gameObject);
        if (prompt != null) prompt.transform.parent.gameObject.SetActive(false);
        GameFlow.Paused = false;
        GameFlow.ShowingResults = false;
        GameFlow.PlayerDead = false;
        Time.timeScale = 1f;
        SceneManager.sceneLoaded += OnLoaded;
        load.allowSceneActivation = true;
    }

    private void OnLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnLoaded;
        StartCoroutine(DropIn());
    }

    // The new area is live under the wall: whip the wall down off the screen (eased in, so it starts slow and snaps away).
    private IEnumerator DropIn()
    {
        yield return null;   // one frame for the new scene's Start()
        Sfx.Play(Sfx.Bank.drop, 0.9f, 0f);
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / SlideTime)
        {
            slider.anchoredPosition = new Vector2(0f, -ScreenHeight * t * t);
            yield return null;
        }
        Instance = null;
        Destroy(gameObject);
    }

    private float ScreenHeight => ((RectTransform)slider.parent).rect.height;   // read live: the canvas isn't laid out on its first frame

    // Entering the portal: the wall drops in over the screen from the top.
    private IEnumerator SlideIn()
    {
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / SlideTime)
        {
            slider.anchoredPosition = new Vector2(0f, ScreenHeight * (1f - t) * (1f - t));
            yield return null;
        }
        slider.anchoredPosition = Vector2.zero;
    }
}
