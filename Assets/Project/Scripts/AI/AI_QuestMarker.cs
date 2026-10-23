using TMPro;
using UnityEngine;

namespace ProjectShaman.AI
{
    // Floating quest symbol above an object, facing the local camera: "!" an offer waits, "?" a quest is finished here
    public class AI_QuestMarker : MonoBehaviour
    {
        public const string OFFER = "!";
        public const string IN_PROGRESS = "?";

        private TextMeshPro _text;
        private Vector3 _localPosition;

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
