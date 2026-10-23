using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ProjectShaman.AI;

// Villager speech bubbles drawn as screen-space UI above each speaker's head (local client only).
// Fixed pixel size at any distance, never hidden behind bodies or walls; hidden when the speaker is behind the camera
// or farther than Max Distance. Placed in the game scene once; bubbles are pooled copies of the inactive template.
public class SpeechBubbleOverlay : MonoBehaviour
{
    private const float HEAD_HEIGHT = 2f;
    private const float SCREEN_MARGIN = 8f;

    [SerializeField] private RectTransform _template;
    [SerializeField, Tooltip("Pixels between the head and the bubble's bottom edge (reference 1920x1080)")]
    private float _headOffset = 24f;
    [SerializeField] private float _maxWidth = 520f;
    [SerializeField, Tooltip("Speakers farther than this (m) from the camera show no bubble; 0 = no limit")]
    private float _maxDistance = 25f;

    [Header("Name Tags")]
    [SerializeField] private bool _showNames = true;
    [SerializeField, Tooltip("Villagers farther than this (m) from the camera show no name; 0 = no limit")]
    private float _nameMaxDistance = 15f;
    [SerializeField] private float _nameFontScale = 0.7f;
    [SerializeField, Range(0f, 1f)] private float _nameBackgroundAlpha = 0.35f;

    private class Bubble
    {
        public RectTransform Rect;
        public TMP_Text Text;
        public LayoutElement TextLayout;
        public Transform Speaker;
        public float HideTime;
        public float Distance;
    }

    private static SpeechBubbleOverlay _instance;

    private readonly List<Bubble> _active = new List<Bubble>();
    private readonly List<Bubble> _free = new List<Bubble>();
    private readonly List<Bubble> _names = new List<Bubble>();
    private RectTransform _area;

    public static void Show(Transform speaker, string line, float seconds)
    {
        if (speaker == null || string.IsNullOrEmpty(line) || !TryGetInstance())
        {
            return;
        }

        _instance.Say(speaker, line, seconds);
    }

    public static void ShowName(Transform target, string displayName)
    {
        if (target == null || string.IsNullOrEmpty(displayName) || !TryGetInstance())
        {
            return;
        }

        _instance.SetName(target, displayName);
    }

    public static void HideName(Transform target)
    {
        if (_instance != null)
        {
            _instance.RemoveName(target);
        }
    }

    private static bool TryGetInstance()
    {
        if (_instance != null)
        {
            return true;
        }

        _instance = FindFirstObjectByType<SpeechBubbleOverlay>(FindObjectsInactive.Include);
        if (_instance == null)
        {
            Debug.LogWarning("[SpeechBubbleOverlay] No SpeechBubbleOverlay in the scene: speech bubbles and name tags are not shown");
            return false;
        }

        return true;
    }

    private void Awake()
    {
        _area = (RectTransform)transform;
        if (_template != null)
        {
            _template.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Say(Transform speaker, string line, float seconds)
    {
        if (_template == null)
        {
            return;
        }

        if (_area == null)
        {
            _area = (RectTransform)transform;
        }

        // One bubble per speaker: a new line replaces the old one
        Bubble bubble = _active.Find(b => b.Speaker == speaker);
        if (bubble == null)
        {
            bubble = Rent();
            bubble.Speaker = speaker;
            _active.Add(bubble);
        }

        bubble.Text.text = line;
        bubble.TextLayout.preferredWidth = Mathf.Min(bubble.Text.GetPreferredValues(line).x, _maxWidth);
        bubble.HideTime = Time.time + seconds;
    }

    private void SetName(Transform target, string displayName)
    {
        if (_template == null)
        {
            return;
        }

        Bubble tag = _names.Find(b => b.Speaker == target);
        if (tag == null)
        {
            tag = CreateNameTag();
            tag.Speaker = target;
            _names.Add(tag);
        }

        tag.Text.text = displayName;
        tag.TextLayout.preferredWidth = Mathf.Min(tag.Text.GetPreferredValues(displayName).x, _maxWidth);
    }

    private void RemoveName(Transform target)
    {
        for (int i = _names.Count - 1; i >= 0; i--)
        {
            if (_names[i].Speaker == target)
            {
                Destroy(_names[i].Rect.gameObject);
                _names.RemoveAt(i);
            }
        }
    }

    private Bubble CreateNameTag()
    {
        Bubble tag = Rent();
        tag.Rect.name = "NameTag";
        tag.Text.fontSize *= _nameFontScale;

        if (tag.Rect.TryGetComponent(out Image background))
        {
            Color color = background.color;
            color.a = _nameBackgroundAlpha;
            background.color = color;
        }

        return tag;
    }

    private Bubble Rent()
    {
        if (_free.Count > 0)
        {
            Bubble reused = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            return reused;
        }

        RectTransform rect = Instantiate(_template, _template.parent);
        TMP_Text text = rect.GetComponentInChildren<TMP_Text>(true);
        LayoutElement layout = text.GetComponent<LayoutElement>();
        if (layout == null)
        {
            layout = text.gameObject.AddComponent<LayoutElement>();
        }

        return new Bubble { Rect = rect, Text = text, TextLayout = layout };
    }

    private void LateUpdate()
    {
        Camera viewer = AI_ViewerCamera.Current;
        PlaceNames(viewer);

        if (_active.Count == 0)
        {
            return;
        }

        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Bubble bubble = _active[i];
            if (bubble.Speaker == null || Time.time >= bubble.HideTime || viewer == null)
            {
                bubble.Rect.gameObject.SetActive(false);
                bubble.Speaker = null;
                _active.RemoveAt(i);
                _free.Add(bubble);
                continue;
            }

            bubble.Rect.gameObject.SetActive(Place(bubble, viewer, _maxDistance, NameHeight(bubble.Speaker)));
        }

        // Nearer speakers draw on top of farther ones
        _active.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        for (int i = 0; i < _active.Count; i++)
        {
            _active[i].Rect.SetSiblingIndex(_template.GetSiblingIndex() + 1 + i);
        }
    }

    private void PlaceNames(Camera viewer)
    {
        for (int i = _names.Count - 1; i >= 0; i--)
        {
            Bubble tag = _names[i];
            if (tag.Speaker == null)
            {
                Destroy(tag.Rect.gameObject);
                _names.RemoveAt(i);
                continue;
            }

            tag.Rect.gameObject.SetActive(_showNames && viewer != null && tag.Speaker.gameObject.activeInHierarchy && Place(tag, viewer, _nameMaxDistance, 0f));
        }
    }

    private float NameHeight(Transform speaker)
    {
        Bubble tag = _names.Find(b => b.Speaker == speaker);
        return tag != null && tag.Rect.gameObject.activeSelf ? tag.Rect.rect.height + SCREEN_MARGIN * 0.5f : 0f;
    }

    private bool Place(Bubble bubble, Camera viewer, float maxDistance, float extraOffset)
    {
        Vector3 head = bubble.Speaker.position + Vector3.up * HEAD_HEIGHT;
        Vector3 screen = viewer.WorldToScreenPoint(head);
        bubble.Distance = screen.z;

        if (screen.z <= 0f || (maxDistance > 0f && screen.z > maxDistance))
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_area, screen, null, out Vector2 local))
        {
            return false;
        }

        // Keep the whole bubble on screen when the head is near an edge
        Rect area = _area.rect;
        Vector2 size = bubble.Rect.rect.size;
        local.y += _headOffset + extraOffset;
        local.x = Mathf.Clamp(local.x, area.xMin + size.x * 0.5f + SCREEN_MARGIN, area.xMax - size.x * 0.5f - SCREEN_MARGIN);
        local.y = Mathf.Clamp(local.y, area.yMin + SCREEN_MARGIN, area.yMax - size.y - SCREEN_MARGIN);
        bubble.Rect.anchoredPosition = local;
        return true;
    }

#if UNITY_EDITOR
    private const string LAYOUT_FONT_PATH = "Assets/ThirdParty/Font/ChosunCentennial_ttf SDF.asset";

    // One-time scene setup: a screen-space canvas on this object and the bubble template. Restyle freely afterwards.
    [ContextMenu("Build Default Layout")]
    private void BuildDefaultLayout()
    {
        RectTransform rootRect = gameObject.GetComponent<RectTransform>();
        if (rootRect == null)
        {
            rootRect = gameObject.AddComponent<RectTransform>();
        }

        if (GetComponent<Canvas>() == null)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        GameObject templateObject = new GameObject("BubbleTemplate", typeof(RectTransform));
        UnityEditor.Undo.RegisterCreatedObjectUndo(templateObject, "Build Default Layout");
        RectTransform template = (RectTransform)templateObject.transform;
        template.SetParent(transform, false);
        template.anchorMin = template.anchorMax = new Vector2(0.5f, 0.5f);
        template.pivot = new Vector2(0.5f, 0f);

        Image background = templateObject.AddComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);
        background.raycastTarget = false;

        HorizontalLayoutGroup layout = templateObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 8, 8);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = templateObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject textObject = new GameObject("Line", typeof(RectTransform));
        textObject.transform.SetParent(template, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LAYOUT_FONT_PATH);
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = 26f;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        textObject.AddComponent<LayoutElement>();

        _template = template;
        templateObject.SetActive(false);
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif
}
