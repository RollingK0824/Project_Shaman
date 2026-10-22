using UnityEngine;

namespace ProjectShaman.AI
{
    // A villager's spoken line, shown on this client by the scene's SpeechBubbleOverlay (screen-space, above the head)
    public class AI_SpeechBubble : MonoBehaviour
    {
        [SerializeField] private float _showSeconds = 2.3f;

        public void Show(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return;
            }

            SpeechBubbleOverlay.Show(transform, line, _showSeconds);
        }

        [ContextMenu("Test Show")]
        private void TestShow()
        {
            SpeechBubbleOverlay.Show(transform, "말풍선 테스트, 오늘 밭이 덥구먼", 10f);
        }
    }
}
