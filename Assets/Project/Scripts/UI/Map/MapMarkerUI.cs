using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MapMarkerUI : MonoBehaviour
{
    [SerializeField]
    private RectTransform _arrow;

    [SerializeField]
    private Image _dot;

    [SerializeField]
    private TMP_Text _label;

    [SerializeField]
    private float _arrowDistance = 16f;     // how far from the dot's centre the arrow sits

    [SerializeField]
    private float _localScale = 1.3f;

    // uv: 0..1 on the map picture. angle: degrees, 0 = up on the picture, NaN = no arrow
    public void Set(Vector2 uv, float angle, Color color, string label, bool isLocal)
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = uv;
        rect.anchorMax = uv;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one * (isLocal ? _localScale : 1f);

        bool showArrow = !float.IsNaN(angle);
        _arrow.gameObject.SetActive(showArrow);
        if (showArrow)
        {
            // Orbit the dot: sit on the facing side and point outwards
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            _arrow.localRotation = rotation;
            _arrow.anchoredPosition = rotation * Vector2.up * _arrowDistance;
        }

        _dot.color = color;
        _label.text = label;
    }
}
