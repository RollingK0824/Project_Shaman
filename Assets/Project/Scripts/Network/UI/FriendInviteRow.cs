using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 친구 초대 패널의 한 줄: 이름 / 상태 / [초대]
public class FriendInviteRow : MonoBehaviour
{
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private Button _inviteButton;
    [SerializeField] private TMP_Text _inviteButtonText;

    private CSteamID _friendId;
    private System.Func<CSteamID, bool> _onInvite;

    public void Bind(SteamLobbyService.FriendInfo friend, System.Func<CSteamID, bool> onInvite)
    {
        _friendId = friend.Id;
        _onInvite = onInvite;

        _nameText.text = friend.Name;
        _stateText.text = friend.InThisGame ? "게임 중" : friend.State == EPersonaState.k_EPersonaStateOnline ? "온라인" : "자리 비움";

        _inviteButton.interactable = true;
        _inviteButtonText.text = "초대";
        _inviteButton.onClick.RemoveAllListeners();
        _inviteButton.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        bool sent = _onInvite != null && _onInvite(_friendId);

        // 같은 친구에게 연달아 보내지 않도록 한 번 보내면 잠근다
        _inviteButton.interactable = !sent;
        _inviteButtonText.text = sent ? "보냄" : "실패";
    }
}