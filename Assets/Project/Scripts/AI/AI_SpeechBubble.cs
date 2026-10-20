using TMPro;
using UnityEngine;

namespace ProjectShaman.AI
{
    public class AI_SpeechBubble : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private float _height = 2.4f;
        [SerializeField] private float _fontSize = 4f;
        [SerializeField] private float _maxWidth = 6f;
        [SerializeField] private float _showSeconds = 2.3f;
        [SerializeField] private float _hearingDistance = 0f;
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _outlineColor = Color.black;
        [SerializeField, Range(0f, 1f)] private float _outlineWidth = 0.25f;

        private TextMeshPro _text;
        private float _hideTime;

        public void Show(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            EnsureText();
            _text.text = line;
            _text.gameObject.SetActive(true);
            _hideTime = Time.time + _showSeconds;
        }

        [ContextMenu("Test Show")]
        private void TestShow()
        {
            _showSeconds = Mathf.Max(_showSeconds, 10f);
            Show("말풍선 테스트, 오늘 밭이 덥구먼");
        }

        private void LateUpdate()
        {
            if (_text == null || !_text.gameObject.activeSelf)
            {
                return;
            }

            Camera viewer = Camera.main;
            if (Time.time >= _hideTime || viewer == null)
            {
                _text.gameObject.SetActive(false);
                return;
            }

            bool inRange = _hearingDistance <= 0f || (viewer.transform.position - transform.position).sqrMagnitude <= _hearingDistance * _hearingDistance;
            _text.enabled = inRange;
            _text.transform.position = transform.position + Vector3.up * _height;
            _text.transform.rotation = viewer.transform.rotation;
        }

        private void EnsureText()
        {
            if (_text != null)
            {
                return;
            }

            GameObject textObject = new GameObject("SpeechBubble");
            textObject.transform.SetParent(transform, false);
            textObject.transform.localPosition = Vector3.up * _height;

            _text = textObject.AddComponent<TextMeshPro>();

            if (_font != null)
            {
                _text.font = _font;
            }

            _text.fontSize = _fontSize;
            _text.alignment = TextAlignmentOptions.Bottom;
            _text.textWrappingMode = TextWrappingModes.Normal;
            _text.rectTransform.sizeDelta = new Vector2(_maxWidth, 2f);
            _text.color = _textColor;
            _text.outlineColor = _outlineColor;
            _text.outlineWidth = _outlineWidth;
        }
    }
}
