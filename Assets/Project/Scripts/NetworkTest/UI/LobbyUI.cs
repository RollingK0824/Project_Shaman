using UnityEngine;
using Mirror;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [SerializeField]
    private Transform _playerListParent;

    [SerializeField]
    private GameObject _playerRowPrefab;

    [SerializeField]
    private TMP_Text _readyButtonText;

    // Update is called once per frame
    void Update()
    {
        RefreshUI();
    }

    public void OnGUI()
    {

    }

    public void RefreshUI()
    {
        // 1. 기존 자식들을 즉시 모두 제거
        for (int i = _playerListParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(_playerListParent.GetChild(i).gameObject);
        }

        // 2. roomSlots 순회하며 생성
        var roomManager = NetworkManager.singleton as NetworkRoomManager;
        if (roomManager == null)
        {
            return;
        }

        foreach (var slot in roomManager.roomSlots)
        {
            var rp = slot as RoomPlayer;
            if (rp == null)
            {
                continue;
            }

            var row = Instantiate(_playerRowPrefab, _playerListParent);
            row.transform.localScale = Vector3.one;

            row.GetComponent<LobbyPlayerRowUI>().SetPlayerInfo(string.IsNullOrEmpty(rp.nickname) ? "Connecting..." : rp.nickname, rp.readyToBegin);

        }
    }

    // Ready 버튼에 연결
    public void OnClickReadyButton()
    {
        var localRoomPlayer = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
        if (localRoomPlayer != null)
        {
            localRoomPlayer.CmdChangeReadyState(!localRoomPlayer.readyToBegin);
        }
    }
}
