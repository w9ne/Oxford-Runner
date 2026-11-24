using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small uGUI toolkit for the menus (main menu, pause menu). Everything is built in code so menus
/// need no prefabs. Shared look: dark panels, wide flat buttons that light up on hover.
/// </summary>
public static class MenuUI
{
    public static readonly Color Accent = new Color(0.55f, 0.75f, 1f);
    public static readonly Color ButtonColor = new Color(0.1f, 0.12f, 0.18f, 0.9f);
    public static readonly Color PanelColor = new Color(0.03f, 0.04f, 0.07f, 0.85f);

    private static Font font;
    /// <summary>The game's UI font: Cinzel (fantasy serif, SIL OFL, Resources/Fonts), falling back to the built-in font.</summary>
    public static Font Font
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("Fonts/Cinzel");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas Canvas(string name, int order)
    {
        var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();
        return canvas;
    }

    public static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    /// <summary>A rect anchored at the centre of its parent, offset by pos.</summary>
    public static RectTransform Rect(Transform parent, string name, Vector2 pos, Vector2 size, Color? color = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        if (color.HasValue) go.AddComponent<Image>().color = color.Value;
        return rt;
    }

    public static RectTransform FullScreen(Transform parent, string name, Color color)
    {
        var rt = Rect(parent, name, Vector2.zero, Vector2.zero, color);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        return rt;
    }

    public static Text Label(Transform parent, string text, int size, Vector2 pos, Vector2 box, Color color,
                             TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
    {
        var rt = Rect(parent, "Label", pos, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = Font;
        t.text = text;
        t.fontSize = size;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;   // Cinzel has tall line metrics; never clip
        return t;
    }

    public static Button Button(Transform parent, string text, Vector2 pos, Action onClick, float width = 420f)
    {
        var rt = Rect(parent, "Button_" + text, pos, new Vector2(width, 70f), ButtonColor);
        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.6f, 1.8f, 2.4f);   // tints the dark button toward the accent
        colors.pressedColor = new Color(2.2f, 2.4f, 3f);
        colors.selectedColor = Color.white;
        colors.colorMultiplier = 2.5f;
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        // Thin accent strip on the left edge.
        var strip = Rect(rt, "Strip", Vector2.zero, new Vector2(6f, 0f), Accent);
        strip.anchorMin = new Vector2(0f, 0f);
        strip.anchorMax = new Vector2(0f, 1f);
        strip.pivot = new Vector2(0f, 0.5f);
        strip.GetComponent<Image>().raycastTarget = false;

        Label(rt, text.ToUpperInvariant(), 30, Vector2.zero, new Vector2(width, 70f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        return button;
    }

    /// <summary>Horizontal slider with a value readout. Calls onChange with the new value.</summary>
    public static Slider Slider(Transform parent, string label, Vector2 pos, float min, float max, float value,
                                Action<float> onChange, string format = "0.0")
    {
        var root = Rect(parent, "Slider_" + label, pos, new Vector2(620f, 60f));
        Label(root, label, 26, new Vector2(-200f, 0f), new Vector2(220f, 60f), Color.white, TextAnchor.MiddleLeft);
        var readout = Label(root, value.ToString(format), 26, new Vector2(270f, 0f), new Vector2(80f, 60f), Accent, TextAnchor.MiddleRight, FontStyle.Bold);

        var track = Rect(root, "Track", new Vector2(60f, 0f), new Vector2(300f, 10f), new Color(1f, 1f, 1f, 0.15f));
        var fillArea = Rect(track, "Fill Area", Vector2.zero, Vector2.zero);
        fillArea.anchorMin = Vector2.zero; fillArea.anchorMax = Vector2.one;
        var fill = Rect(fillArea, "Fill", Vector2.zero, Vector2.zero, Accent);
        fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0f, 1f);
        var handleArea = Rect(track, "Handle Area", Vector2.zero, Vector2.zero);
        handleArea.anchorMin = Vector2.zero; handleArea.anchorMax = Vector2.one;
        var handle = Rect(handleArea, "Handle", Vector2.zero, new Vector2(22f, 34f), Color.white);
        handle.anchorMin = new Vector2(0f, 0f); handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(22f, 24f);

        var slider = track.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.onValueChanged.AddListener(v => { readout.text = v.ToString(format); onChange(v); });
        return slider;
    }

    /// <summary>The settings card shared by the main menu and the pause menu.</summary>
    public static RectTransform SettingsPanel(Transform parent, Action onBack)
    {
        var panel = Rect(parent, "Settings", Vector2.zero, new Vector2(760f, 420f), PanelColor);
        Label(panel, "SETTINGS", 44, new Vector2(0f, 150f), new Vector2(700f, 60f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        Slider(panel, "Mouse sensitivity", new Vector2(0f, 30f), GameSettings.MinSensitivity, GameSettings.MaxSensitivity,
               GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v);
        Button(panel, "Back", new Vector2(0f, -130f), onBack, 300f);
        return panel;
    }
}
