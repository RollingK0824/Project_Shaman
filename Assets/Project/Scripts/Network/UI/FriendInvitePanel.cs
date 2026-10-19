using Mirror;
using TMPro;
using UnityEngine;

// 오버레이 없이 쓰는 친구 초대 창. 대기실 [친구 초대] 버튼으로 연다.
public class FriendInvitePanel : MonoBehaviour
{
    [SerializeField] private Transform _rowParent;
    [SerializeField] private FriendInviteRow _rowPrefab;
    [SerializeField] private TMP_Text _emptyText;

    public void Open()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        foreach (Transform child in _rowParent)
        {
            Destroy(child.gameObject);
        }

        SteamLobbyService service = GetService();

        if (service == null || !service.CanInvite)
        {
            ShowEmpty("방을 만든 호스트만 초대할 수 있습니다.");
            return;
        }

        var friends = service.GetOnlineFriends();

        if (friends.Count == 0)
        {
            ShowEmpty("접속 중인 친구가 없습니다.");
            return;
        }

        _emptyText.gameObject.SetActive(false);

        foreach (var friend in friends)
        {
            FriendInviteRow row = Instantiate(_rowPrefab, _rowParent);
            row.Bind(friend, service.InviteFriend);
        }
    }

    private void ShowEmpty(string message)
    {
        _emptyText.gameObject.SetActive(true);
        _emptyText.text = message;
    }

    private static SteamLobbyService GetService()
    {
        return NetworkManager.singleton != null
            ? NetworkManager.singleton.GetComponent<SteamLobbyService>()
            : null;
    }
}