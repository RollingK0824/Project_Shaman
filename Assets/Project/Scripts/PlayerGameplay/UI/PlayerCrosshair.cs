using UnityEngine;
using UnityEngine.UI;

public class PlayerCrosshair : MonoBehaviour
{
    [SerializeField] private Image _dot;
    [SerializeField, Min(1)] private float _size = 4f;
    [SerializeField, Range(0, 1)] private float _alpha = .7f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _highlightColor = new Color(1f, .8f, .45f);
    private bool _highlighted;
    public void SetHighlighted(bool highlighted) { _highlighted = highlighted; Refresh(); }
    private void OnEnable() => Refresh();
    private void OnValidate() => Refresh();
    private void Refresh()
    {
        if (_dot == null) return;
        _dot.rectTransform.sizeDelta = Vector2.one * _size;
        var color = _highlighted ? _highlightColor : _normalColor; color.a = _alpha; _dot.color = color;
    }
}
