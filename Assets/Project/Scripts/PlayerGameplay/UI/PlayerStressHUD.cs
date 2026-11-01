using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(2000)]
public class PlayerStressHUD : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private PlayerStressController _stress;
    [SerializeField] private PlayerHealth _health;

    [Header("Display")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _valueText;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private bool _hideOnDeath = true;

    [Header("Stage Colors")]
    [SerializeField] private Color _lowColor = new Color(0.57f, 0.77f, 0.68f);
    [SerializeField] private Color _midColor = new Color(0.94f, 0.72f, 0.36f);
    [SerializeField] private Color _highColor = new Color(0.96f, 0.39f, 0.34f);

    private void Awake()
    {
        if (_canvas == null) _canvas = GetComponent<Canvas>();
        StylePanel();
    }

    private void StylePanel()
    {
        if (_valueText == null || _fill == null || _levelText == null) return;
        var panel = (RectTransform)_valueText.transform.parent;
        if (panel.TryGetComponent<Image>(out var background)) background.color = Color.clear;
        PlayerHUDFrame.Attach(panel);
        var rule = panel.Find("TopRule");
        if (rule != null) rule.gameObject.SetActive(false);
        var title = panel.Find("Title")?.GetComponent<TMP_Text>();
        PlaceLabel(title, new Vector2(18, -8), new Vector2(130, 34), 23);
        PlaceLabel(_valueText, new Vector2(158, -12), new Vector2(124, 26), 18);
        _valueText.alignment = TextAlignmentOptions.Right;
        PlaceLabel(_levelText, new Vector2(18, -46), new Vector2(58, 22), 17);
        var gauge = (RectTransform)_fill.transform.parent;
        PlayerItemUIStyle.Place(gauge, new Vector2(0, 1), new Vector2(0, 1), new Vector2(82, -49), new Vector2(200, 14));
        // Remove the old gold Outline, including the outline that made an empty gauge look filled.
        foreach (var effect in gauge.GetComponentsInChildren<BaseMeshEffect>(true)) effect.enabled = false;
        var art = PlayerHUDArt.Current;
        if (gauge.TryGetComponent<Image>(out var track))
        {
            track.sprite = art != null ? art.RedBrush : null;
            track.color = new Color(1, 1, 1, .22f);
        }
        if (art != null) _fill.sprite = art.RedBrush;
        _lowColor = new Color(.76f, .83f, .81f);
        _midColor = new Color(.92f, .80f, .70f);
        _highColor = new Color(.96f, .39f, .34f);
    }

    private static void PlaceLabel(TMP_Text label, Vector2 position, Vector2 size, float fontSize)
    {
        if (label == null) return;
        PlayerItemUIStyle.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), position, size);
        var art = PlayerHUDArt.Current;
        PlayerItemUIStyle.Label(label, art != null ? art.Font : label.font, fontSize);
        label.color = PlayerHUDArt.Text;
        label.alignment = TextAlignmentOptions.Left;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    public PlayerStatusHUD StatusHUD { get; private set; }

    private QuickSlotUI _quickSlots;
    private readonly Vector3[] _corners = new Vector3[4];
    private void LateUpdate()
    {
        if (_valueText == null || _stress == null) return;
        var panel = _valueText.transform.parent as RectTransform;
        if (panel == null || !(panel.parent is RectTransform parent)) return;
        var inventory = _stress.GetComponent<PlayerInventory>();
        if (_quickSlots == null || _quickSlots.BoundInventory != inventory)
            foreach (var quick in FindObjectsByType<QuickSlotUI>(FindObjectsSortMode.None))
                if (quick.BoundInventory == inventory) { _quickSlots = quick; break; }
        float unit = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        float left = Screen.width * .5f - 280f * unit, centerY = 62f * unit;
        if (_quickSlots != null)
        {
            ((RectTransform)_quickSlots.transform).GetWorldCorners(_corners);
            var qc = _quickSlots.GetComponentInParent<Canvas>();
            Camera camera = qc.renderMode == RenderMode.ScreenSpaceOverlay ? null : qc.worldCamera;
            var bottom = RectTransformUtility.WorldToScreenPoint(camera, _corners[0]);
            var top = RectTransformUtility.WorldToScreenPoint(camera, _corners[1]);
            left = bottom.x; centerY = (bottom.y + top.y) * .5f;
        }
        float scale = Mathf.Min(unit, Mathf.Max(.1f, (left - 48f * unit) / 300f));
        Vector2 screen = new Vector2(24f * unit + 150f * scale, Mathf.Max(12f * unit + 42f * scale, centerY));
        var canvas = parent.GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
        // Write only on change: transform writes dirty the whole canvas even with equal values.
        Vector2 center = new Vector2(.5f, .5f), size = new Vector2(300, 84);
        Vector3 localScale = Vector3.one * scale / Mathf.Max(.001f, Mathf.Abs(parent.lossyScale.x));
        if (panel.anchorMin != center) panel.anchorMin = center;
        if (panel.anchorMax != center) panel.anchorMax = center;
        if (panel.pivot != center) panel.pivot = center;
        if (panel.sizeDelta != size) panel.sizeDelta = size;
        if (panel.anchoredPosition != local) panel.anchoredPosition = local;
        if (panel.localScale != localScale) panel.localScale = localScale;
    }

    public void Bind(PlayerStressController stress, PlayerHealth health)
    {
        Unsubscribe();
        _stress = stress;
        _health = health;
        if (StatusHUD == null) StatusHUD = GetComponent<PlayerStatusHUD>() ?? gameObject.AddComponent<PlayerStatusHUD>();
        StatusHUD.Bind(stress != null ? stress.gameObject : null, _valueText != null ? _valueText.font : null);
        if (isActiveAndEnabled) Subscribe();
        Refresh();
    }

    private void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (_stress != null)
        {
            _stress.StressChanged += HandleStressChanged;
            _stress.StressLevelChanged += HandleLevelChanged;
        }
        if (_health != null) _health.Died += HandleDied;
    }

    private void OnDisable()
    {
        Unsubscribe();
        if (_canvas != null) _canvas.enabled = false;
    }

    private void Unsubscribe()
    {
        if (_stress != null)
        {
            _stress.StressChanged -= HandleStressChanged;
            _stress.StressLevelChanged -= HandleLevelChanged;
        }
        if (_health != null) _health.Died -= HandleDied;
    }

    private void HandleStressChanged(float value) => Refresh();
    private void HandleLevelChanged(PlayerStressController.StressLevel level) => Refresh();
    private void HandleDied(GameObject source) => Refresh();

    private void Refresh()
    {
        if (_canvas != null)
            _canvas.enabled = isActiveAndEnabled && _stress != null &&
                (!_hideOnDeath || _health == null || !_health.IsDead);
        if (_stress == null) return;

        Color color = _lowColor;
        string label = "안정";
        if (_stress.CurrentLevel == PlayerStressController.StressLevel.Mid)
        {
            color = _midColor;
            label = "긴장";
        }
        else if (_stress.CurrentLevel == PlayerStressController.StressLevel.High)
        {
            color = _highColor;
            label = "위험";
        }

        if (_valueText != null) _valueText.SetText("{0:1} / 100", _stress.CurrentStress);
        if (_levelText != null)
        {
            _levelText.text = label;
            _levelText.color = color;
        }
        if (_fill != null)
        {
            _fill.color = Color.white;
            _fill.rectTransform.anchorMax = new Vector2(_stress.NormalizedStress, 1f);
        }
    }
}
