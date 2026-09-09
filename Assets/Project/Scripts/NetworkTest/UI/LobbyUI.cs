using UnityEngine;
using Mirror;
using TMPro;

public class LobbyUI : MonoBehaviour
{
    [SerializeField]
    private Transform _playerListParent;

    [SerializeField]
    private GameObject _playerRowPrefab;

    // Update is called once per frame
    void Update()
    {
        // 프로토타입 단계여서 매 프레임 새로고침 --> 나중에 이벤트 기반으로 최적화 가능
        foreach (Transform child in _playerListParent)
        {
            Destroy(child.gameObject);
        }

        // 대기실에 참여한 유저(RoomPlayer) 목록을 순회하며 닉네임과 레디 상태를 UI에 표시
        //foreach (var slot in NetworkRoomManager.singleton.roomSlots)
        //{
        //    var rp = slot as RoomPlayer;
        //    if (rp == null)
        //    {
        //        continue;
        //    }
        //
        //    var row = Instantiate(_playerRowPrefab, _playerListParent);
        //    row.GetComponentInChildren<TMP_Text>().text = $"{rp.nickname} {(rp.readyToBegin ? \"[Ready]\" : \"\")}";
        //}
    }

    // Ready 버튼에 연결
    public void OnClickReadyButton()
    {
        var localRoomPlayer = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
        localRoomPlayer.CmdChangeReadyState(!localRoomPlayer.readyToBegin);
    }
}
