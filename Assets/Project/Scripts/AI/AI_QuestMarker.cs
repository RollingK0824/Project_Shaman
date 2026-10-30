using TMPro;
using UnityEngine;

namespace ProjectShaman.AI
{
    // Floating quest symbol above an object, facing the local camera: "!" an offer waits, "?" a quest is finished here
    public class AI_QuestMarker : MonoBehaviour
    {
        public const string OFFER = "!";
        public const string IN_PROGRESS = "?";

        private static readonly System.Collections.Generic.List<AI_QuestMarker> _shown = new System.Collections.Generic.List<AI_QuestMarker>();

        private TextMeshPro _text;
        private Vector3 _localPosition;

        // Markers on screen now (the HUD compass points at them)
        public static System.Collections.Generic.IReadOnlyList<AI_QuestMarker> Shown => _shown;
        public string Symbol => _text != null ? _text.text : null;
        public Color Color => _text != null ? _text.color : Color.white;

        private void OnEnable()
        {
            if (!_shown.Contains(this))
            {
                _shown.Add(this);
            }
        }

        private void OnDisable()
        {
            _shown.Remove(this);
        }

        public static AI_QuestMarker Create(Transform parent, Vector3 localPosition, float fontSize, Color color)
        {
            GameObject markerObject = new GameObject("QuestMarker");
            markerObject.transform.SetParent(parent, false);
            markerObject.transform.localPosition = localPosition;

            AI_QuestMarker marker = markerObject.AddComponent<AI_QuestMarker>();
            marker._localPosition = localPosition;
            marker._text = markerObject.AddComponent<TextMeshPro>();
            marker._text.fontSize = fontSize;
            marker._text.fontStyle = FontStyles.Bold;
            marker._text.alignment = TextAlignmentOptions.Center;
            marker._text.color = color;
            marker._text.outlineColor = Color.black;
            marker._text.outlineWidth = 0.3f;
            markerObject.SetActive(false);
            return marker;
        }

        public void SetColor(Color color)
        {
            _text.color = color;
        }

        // null or empty hides the marker
        public void Show(string symbol)
        {
            bool visible = !string.IsNullOrEmpty(symbol);
            if (visible)
            {
                _text.text = symbol;
            }

            gameObject.SetActive(visible);
        }

        private void LateUpdate()
        {
            transform.localPosition = _localPosition;
            AI_ViewerCamera.Face(transform);
        }
    }
}
