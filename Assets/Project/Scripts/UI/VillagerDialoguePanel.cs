using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local-only window for talking to a resting villager: the villager's line plus question buttons.
// Built in code (no prefab); opened through UIManager so gameplay input is blocked and the cursor freed.
// Closing it (Esc, the bye question, or the server) reports back through onClosed.
public class VillagerDialoguePanel : UIPanel
{
    private const float WIDTH = 820f;
    private const float BUTTON_HEIGHT = 46f;

    private static VillagerDialoguePanel _instance;

    private TMP_Text _speaker;
    private TMP_Text _line;
    private RectTransform _buttons;
    private TMP_FontAsset _font;
    private Action<string> _onPick;
    private Action _onClosed;
    private bool _closingQuietly;

    public static bool IsShowing => _instance != null && _instance.gameObject.activeSelf;

    public static void Show(TMP_FontAsset font, string speaker, string line, string[] topicIds, string[] labels, Action<string> onPick, Action onClosed)
    {
        VillagerDialoguePanel panel = Ensure(font);
        panel._onPick = onPick;
        panel._onClosed = onClosed;
        panel.Fill(speaker, line, topicIds, labels);

        if (!panel.gameObject.activeSelf)
        {
            panel.Open();
        }
    }

    public static void UpdateContent(string line, string[] topicIds, string[] labels)
    {
        if (IsShowing)
        {
            _instance.Fill(null, line, topicIds, labels);
        }
    }

    // Server ended the talk: show its last line with a single close button, without echoing a close back
    public static void ShowFinal(string line)
    {
        if (!IsShowing)
        {
            return;
        }

        _instance._onClosed = null;
        _instance.Fill(null, line, new[] { string.Empty }, new[] { "닫기" });
        _instance._onPick = _ => _instance.Close();
    }

    public static void CloseQuietly()
    {
        if (IsShowing)
        {
            _instance._closingQuietly = true;
            _instance.Close();
        }
    }

    private void OnDisable()
    {
        Action closed = _onClosed;
        _onClosed = null;
        _onPick = null;

        if (!_closingQuietly)
        {
            closed?.Invoke();
        }

        _closingQuietly = false;
    }

    private void Fill(string speaker, string line, string[] topicIds, string[] labels)
    {
        if (speaker != null)
        {
            _speaker.text = speaker;
        }

        _line.text = line ?? string.Empty;

        foreach (Transform child in _buttons)
        {
            Destroy(child.gameObject);
        }

        int count = topicIds != null && labels != null ? Mathf.Min(topicIds.Length, labels.Length) : 0;
        for (int i = 0; i < count; i++)
        {
            string topicId = topicIds[i];
            CreateButton(labels[i], () => _onPick?.Invoke(topicId));
        }
    }

    private static VillagerDialoguePanel Ensure(TMP_FontAsset font)
    {
        if (_instance != null)
        {
            if (font != null)
            {
                _instance._font = font;
            }

            return _instance;
        }

        GameObject root = new GameObject("VillagerDialoguePanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.SetActive(false);
        DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        VillagerDialoguePanel panel = root.AddComponent<VillagerDialoguePanel>();
        panel._font = font;
        panel.Build(root.transform);
        _instance = panel;
        return panel;
    }

    private void Build(Transform root)
    {
        RectTransform box = NewRect("Box", root);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 0f);
        box.pivot = new Vector2(0.5f, 0f);
        box.anchoredPosition = new Vector2(0f, 60f);
        box.sizeDelta = new Vector2(WIDTH, 0f);
        box.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.08f, 0.06f, 0.92f);

        VerticalLayoutGroup layout = box.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(28, 28, 22, 22);
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        box.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _speaker = NewText("Speaker", box, 26f, new Color(1f, 0.85f, 0.45f));
        _line = NewText("Line", box, 24f, Color.white);

        _buttons = NewRect("Questions", box);
        VerticalLayoutGroup buttonLayout = _buttons.gameObject.AddComponent<VerticalLayoutGroup>();
        buttonLayout.spacing = 6f;
        buttonLayout.childControlHeight = true;
        buttonLayout.childControlWidth = true;
        buttonLayout.childForceExpandHeight = false;
    }

    private void CreateButton(string label, Action onClick)
    {
        RectTransform rect = NewRect("Question", _buttons);
        rect.gameObject.AddComponent<LayoutElement>().minHeight = BUTTON_HEIGHT;
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.2f, 0.15f, 1f);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.45f, 0.36f, 0.24f, 1f);
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        TMP_Text text = NewText("Label", rect, 22f, Color.white);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 0f);
        textRect.offsetMax = new Vector2(-16f, 0f);
        text.text = label;
    }

    private TMP_Text NewText(string name, Transform parent, float size, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (_font != null)
        {
            text.font = _font;
        }

        text.fontSize = size;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
}
