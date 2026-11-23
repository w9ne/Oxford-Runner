using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Runs the movement tutorial: shows each station's prompt as the player reaches it, keeps the
/// checkpoint in sync, tracks completion time (best time persisted in PlayerPrefs), and shows a
/// results card before loading the next scene. Builds its own uGUI overlay at runtime.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public const string BestTimeKey = "BestTime_Tutorial";

    public static TutorialManager Instance { get; private set; }

    [SerializeField] private FirstPersonController player;
    [SerializeField] private float resultsDuration = 3.5f;
    [SerializeField] private Color accent = new Color(1f, 0.78f, 0.3f);

    private TutorialStep[] steps;
    private TutorialStep current;
    private float startTime;
    private bool finished;
    private int respawns;

    // UI
    private CanvasGroup promptGroup;
    private Text stepLabel, titleText, bodyText, extraText, timerText, toastText;
    private CanvasGroup toastGroup;
    private Coroutine promptFade, toastFade;

    private void Awake()
    {
        Instance = this;
        if (player == null) player = FindFirstObjectByType<FirstPersonController>();
        steps = FindObjectsByType<TutorialStep>(FindObjectsSortMode.None).OrderBy(s => s.order).ToArray();
        BuildUI();
        Checkpoints.Clear();
        Checkpoints.Respawned += OnRespawned;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Checkpoints.Respawned -= OnRespawned;
    }

    private void Start()
    {
        startTime = Time.time;
        if (steps.Length > 0) EnterStep(steps[0]);
    }

    private void Update()
    {
        if (!finished) timerText.text = FormatTime(Time.time - startTime);

        if (current != null && current.gate != null && !current.gate.IsOpen)
            extraText.text = $"Targets destroyed: {current.gate.Total - current.gate.Remaining} / {current.gate.Total}";
        else if (current != null && current.gate != null)
            extraText.text = "Gate open!";
    }

    /// <summary>Called by step triggers. Only moves forward, so backtracking doesn't rewind progress.</summary>
    public void EnterStep(TutorialStep step)
    {
        if (finished || step == null) return;
        if (current != null && step.order <= current.order) return;

        current = step;
        Checkpoints.Set(step.Spawn.position, step.Spawn.eulerAngles.y);

        int index = System.Array.IndexOf(steps, step) + 1;
        stepLabel.text = $"STEP {index} / {steps.Length}";
        titleText.text = step.title.ToUpperInvariant();
        bodyText.text = step.instructions;
        extraText.text = "";
        if (promptFade != null) StopCoroutine(promptFade);
        promptFade = StartCoroutine(Pop(promptGroup));
    }

    private void OnRespawned()
    {
        respawns++;
        Toast("Back to checkpoint");
    }

    /// <summary>Called by the exit portal: shows results, saves best time, then loads the next scene.</summary>
    public void Complete(string nextScene)
    {
        if (finished) return;
        finished = true;
        float time = Time.time - startTime;
        float best = PlayerPrefs.GetFloat(BestTimeKey, float.MaxValue);
        bool newBest = time < best;
        if (newBest)
        {
            PlayerPrefs.SetFloat(BestTimeKey, time);
            PlayerPrefs.Save();
            best = time;
        }

        stepLabel.text = "TUTORIAL COMPLETE";
        titleText.text = FormatTime(time) + (newBest ? "  NEW BEST!" : "");
        bodyText.text = $"Best: {FormatTime(best)}     Checkpoint respawns: {respawns}";
        extraText.text = "Loading the arena...";
        if (promptFade != null) StopCoroutine(promptFade);
        promptFade = StartCoroutine(Pop(promptGroup));
        StartCoroutine(LoadAfter(nextScene, resultsDuration));
    }

    private IEnumerator LoadAfter(string scene, float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneManager.LoadScene(scene);
    }

    public static string FormatTime(float seconds)
    {
        int m = (int)(seconds / 60f);
        return $"{m}:{seconds - m * 60f:00.0}";
    }

    // ------------------------------------------------------------------ UI
    private void Toast(string message)
    {
        toastText.text = message;
        if (toastFade != null) StopCoroutine(toastFade);
        toastFade = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        toastGroup.alpha = 1f;
        yield return new WaitForSeconds(1.2f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.5f) { toastGroup.alpha = 1f - t; yield return null; }
        toastGroup.alpha = 0f;
    }

    // Slide-down + fade-in when a new prompt appears.
    private static IEnumerator Pop(CanvasGroup group)
    {
        var rt = (RectTransform)group.transform;
        Vector2 rest = new Vector2(0f, -30f);
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.35f)
        {
            float e = 1f - Mathf.Pow(1f - t, 3f);
            group.alpha = e;
            rt.anchoredPosition = rest + Vector2.up * (30f * (1f - e));
            yield return null;
        }
        group.alpha = 1f;
        rt.anchoredPosition = rest;
    }

    private void BuildUI()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var canvasGO = new GameObject("TutorialUI", typeof(Canvas), typeof(CanvasScaler));
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Prompt card (top centre)
        var card = Panel(canvasGO.transform, "PromptCard", new Vector2(0.5f, 1f), new Vector2(760f, 190f), new Color(0.05f, 0.05f, 0.09f, 0.78f));
        promptGroup = card.gameObject.AddComponent<CanvasGroup>();
        var stripe = Panel(card, "Accent", new Vector2(0.5f, 1f), new Vector2(760f, 5f), accent);
        stripe.anchoredPosition = Vector2.zero;

        stepLabel = Label(card, "Step", font, 20, accent, TextAnchor.UpperLeft, new Vector2(28f, -18f), new Vector2(700f, 28f));
        titleText = Label(card, "Title", font, 40, Color.white, TextAnchor.UpperLeft, new Vector2(28f, -44f), new Vector2(700f, 52f));
        titleText.fontStyle = FontStyle.Bold;
        bodyText = Label(card, "Body", font, 24, new Color(0.88f, 0.88f, 0.92f), TextAnchor.UpperLeft, new Vector2(28f, -100f), new Vector2(704f, 60f));
        extraText = Label(card, "Extra", font, 22, accent, TextAnchor.UpperLeft, new Vector2(28f, -154f), new Vector2(704f, 30f));

        // Timer (top right)
        timerText = Label(canvasGO.transform, "Timer", font, 34, Color.white, TextAnchor.UpperRight, Vector2.zero, new Vector2(300f, 50f));
        var trt = timerText.rectTransform;
        trt.anchorMin = trt.anchorMax = trt.pivot = new Vector2(1f, 1f);
        trt.anchoredPosition = new Vector2(-40f, -30f);
        timerText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);

        // Toast (lower centre)
        toastText = Label(canvasGO.transform, "Toast", font, 28, new Color(1f, 0.55f, 0.45f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(800f, 50f));
        var tort = toastText.rectTransform;
        tort.anchorMin = tort.anchorMax = tort.pivot = new Vector2(0.5f, 0.5f);
        tort.anchoredPosition = new Vector2(0f, 110f);   // just above the crosshair, clear of the book
        toastText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        toastGroup = toastText.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
    }

    private static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, -30f);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rt;
    }

    private static Text Label(Transform parent, string name, Font font, int size, Color color, TextAnchor align,
                              Vector2 topLeft, Vector2 boxSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = topLeft;
        rt.sizeDelta = boxSize;
        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }
}
