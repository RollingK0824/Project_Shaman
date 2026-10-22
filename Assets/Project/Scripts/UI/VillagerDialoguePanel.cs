using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Local-only window for talking to a resting villager: the villager's line plus question buttons.
// Placed in the game scene (inactive) and shown with SetActive through UIManager, so it goes away with the scene.
// Opened through UIManager so gameplay input is blocked and the cursor freed.
// Closing it (Esc, the bye question, or the server) reports back through onClosed.
public class VillagerDialoguePanel : UIPanel
{
    private const float WIDTH = 820f;
    private const float BUTTON_HEIGHT = 46f;

    [SerializeField] private TMP_Text _speaker;
    [SerializeField] private TMP_Text _line;
    [SerializeField] private RectTransform _buttons;
    [SerializeField, Tooltip("Inactive question button under Buttons, copied for each question")]
    private Button _questionTemplate;

    private static VillagerDialoguePanel _instance;

    private Action<string> _onPick;
    private Action _onClosed;
    private bool _closingQuietly;

    public static bool IsShowing => Find() != null && _instance.gameObject.activeSelf;

    public static void Show(string speaker, string line, string[] topicIds, string[] labels, Action<string> onPick, Action onClosed)
    {
        VillagerDialoguePanel panel = Find();
        if (panel == null)
        {
            Debug.LogWarning("[VillagerDialoguePanel] No VillagerDialoguePanel in the scene: place one (inactive) under the game UI");
            onClosed?.Invoke();
            return;
        }

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

    // The panel starts inactive, so Awake never runs before the first talk: look it up in the loaded scene instead
    private static VillagerDialoguePanel Find()
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<VillagerDialoguePanel>(FindObjectsInactive.Include);
        }

        return _instance;
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

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
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
            if (child != _questionTemplate.transform)
            {
                Destroy(child.gameObject);
            }
        }

        int count = topicIds != null && labels != null ? Mathf.Min(topicIds.Length, labels.Length) : 0;
        for (int i = 0; i < count; i++)
        {
            string topicId = topicIds[i];
            CreateButton(labels[i], () => _onPick?.Invoke(topicId));
        }
    }

    private void CreateButton(string label, Action onClick)
    {
        Button button = Instantiate(_questionTemplate, _buttons);
        button.gameObject.SetActive(true);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onClick());

        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = label;
        }
    }

#if UNITY_EDITOR
    private const string LAYOUT_FONT_PATH = "Assets/ThirdParty/Font/ChosunCentennial_ttf SDF.asset";

    // One-time scene setup: builds the default layout under this object and wires the fields. Restyle freely afterwards.
    [ContextMenu("Build Default Layout")]
    private void BuildDefaultLayout()
    {
        if (GetComponentInParent<Canvas>(true) == null)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            gameObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform box = NewRect("Box", transform);
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

        RectTransform question = NewRect("QuestionTemplate", _buttons);
        question.gameObject.AddComponent<LayoutElement>().minHeight = BUTTON_HEIGHT;
        question.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.2f, 0.15f, 1f);
        _questionTemplate = question.gameObject.AddComponent<Button>();
        ColorBlock colors = _questionTemplate.colors;
        colors.highlightedColor = new Color(0.45f, 0.36f, 0.24f, 1f);
        _questionTemplate.colors = colors;

        TMP_Text label = NewText("Label", question, 22f, Color.white);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(16f, 0f);
        label.rectTransform.offsetMax = new Vector2(-16f, 0f);
        question.gameObject.SetActive(false);

        gameObject.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private static TMP_Text NewText(string name, Transform parent, float size, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LAYOUT_FONT_PATH);
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Build Default Layout");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
#endif
}
