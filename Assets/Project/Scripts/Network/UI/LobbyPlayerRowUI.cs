using TMPro;
using UnityEngine;

public class LobbyPlayerRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _readyText;

    private Color _defaultNameColor;
    private bool _hasDefaultColor;

    public void SetPlayerInfo(string nickname, bool isReady, bool isHost, int colorId)
    {
        if (_nameText != null)
        {
            // 프리팹의 원래 글자색을 기억해 두고, 캐릭터 배정 전(-1)이면 그 색을 사용
            if (!_hasDefaultColor)
            {
                _defaultNameColor = _nameText.color;
                _hasDefaultColor = true;
            }

            _nameText.text = nickname;
            _nameText.color = colorId >= 0 ? PlayerColors.Get(colorId) : _defaultNameColor;
        }

        if (_readyText != null)
        {
            _readyText.text = isHost ? "H" : (isReady ? "<color=#00FF00>V</color>" : "<color=#FF0000>X</color>");
        }
    }
}