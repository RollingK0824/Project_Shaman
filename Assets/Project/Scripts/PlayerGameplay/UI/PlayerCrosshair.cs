using UnityEngine;
using UnityEngine.UI;

public class PlayerCrosshair : MonoBehaviour
{
    [SerializeField] private Image _dot;
    [SerializeField, Min(1)] private float _size = 4f;
    [SerializeField, Range(0, 1)] private float _alpha = .7f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _highlightColor = new Color(1f, .8f, .45f);
    [SerializeField, Min(0f)] private float _outlineWidth = 1.2f;
    [SerializeField, Min(1f)] private float _highlightScale = 1.2f;
    private bool _highlighted;
    private GameObject _targetAccent;
    public bool IsHighlighted => _highlighted;
    public void SetHighlighted(bool highlighted) { if (_highlighted == highlighted) return; _highlighted = highlighted; Refresh(); }
    private void OnEnable() => Refresh();
    private void OnValidate() => Refresh();
    private void Refresh()
    {
        if (_dot == null) return;
        if (Application.isPlaying && _targetAccent == null) CreateTargetAccent();
        if (_targetAccent != null) _targetAccent.SetActive(_highlighted);
        _dot.rectTransform.sizeDelta = Vector2.one * _size * (_highlighted ? _highlightScale : 1f);
        var color = _highlighted ? _highlightColor : _normalColor; color.a = _alpha; _dot.color = color;
        var outline = _dot.GetComponent<Outline>();
        if (outline != null) { outline.effectColor = new Color(.03f, .025f, .02f, .95f); outline.effectDistance = new Vector2(_outlineWidth, -_outlineWidth); }
    }
    private void CreateTargetAccent()
    {
        _targetAccent = new GameObject("Interactable Target Accent", typeof(RectTransform));
        _targetAccent.transform.SetParent(_dot.transform, false);
        var rect = (RectTransform)_targetAccent.transform;
        rect.sizeDelta = new Vector2(30, 30);
        foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
        {
            CreateAccentLine(rect, new Vector2(x * 11, y * 15), new Vector2(8, 2));
            CreateAccentLine(rect, new Vector2(x * 15, y * 11), new Vector2(2, 8));
        }
    }
    private void CreateAccentLine(Transform parent, Vector2 position, Vector2 size)
    {
        var line = new GameObject("Corner", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(parent, false);
        var rect = (RectTransform)line.transform;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = line.GetComponent<Image>();
        image.color = _highlightColor; image.raycastTarget = false;
    }
}
