using TMPro;
using UnityEngine;

public class LobbyPlayerRowUI : MonoBehaviour
{
    [SerializeField]
    private TMP_Text _nameText;

    [SerializeField]
    private TMP_Text _readyText;

    public void SetPlayerInfo(string nickname, bool isReady)
    {
        if (_nameText != null)
        {
            _nameText.text = nickname;
        }
        if (_readyText != null)
        {
            _readyText.text = isReady ? "<color=#00FF00>V</color>" : "<color=#FF0000>X</color>";
        }
    }
}
