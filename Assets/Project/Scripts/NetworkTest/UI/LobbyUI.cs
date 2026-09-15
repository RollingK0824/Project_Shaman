using UnityEngine;
using Mirror;
using TMPro;
using System.Collections;
using System.Linq;
using UnityEngine.UI;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] private Transform _playerListParent;

    [SerializeField] private GameObject _playerRowPrefab;

    [SerializeField] private GameObject _leaveConfirmPanel;

    [SerializeField] private TMP_Text _readyButtonText;
    [SerializeField] private Button _readyButton;
    [SerializeField] private TMP_Text _playerCountText;

    [SerializeField] private GameObject _notEnoughPlayerCount;
    [SerializeField] private float _dispalyTime = 2f;



    private Coroutine _warningCoroutine;
    private RoomPlayer _localRoomPlayer;
    void Start()
    {
        _leaveConfirmPanel.SetActive(false);
        _notEnoughPlayerCount.SetActive(false);
    }
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

            row.GetComponent<LobbyPlayerRowUI>().SetPlayerInfo(string.IsNullOrEmpty(rp.nickname) ? "Connecting..." : rp.nickname, rp.readyToBegin, rp.index == 0);

        }

        if (_localRoomPlayer == null)
        {
            _localRoomPlayer = NetworkClient.connection?.identity?.GetComponent<RoomPlayer>();
        }
        

        UpdateReadyButton(roomManager);
    }
    private void UpdateReadyButton(NetworkRoomManager roomManager)
    {
        //var localRoomPlayer = NetworkClient.connection?.identity?.GetComponent<RoomPlayer>();
        if (_localRoomPlayer == null)
        {
            return;
        }

        _playerCountText.text = $"{roomManager.roomSlots.Count} / {_localRoomPlayer.roomMaxPlayers}";

        if (NetworkServer.active)
        {
            _readyButtonText.text = "Start";

            bool enoughPlayers = roomManager.roomSlots.Count >= roomManager.minPlayers;
            bool othersReady = roomManager.roomSlots.OfType<RoomPlayer>().Where(rp => rp != _localRoomPlayer).All(rp => rp.readyToBegin);

            _readyButton.interactable = enoughPlayers && othersReady;
        }
        else
        {
            _readyButtonText.text = _localRoomPlayer.readyToBegin ? "Cancel" : "Ready";
            _readyButton.interactable = true;
        }
    }

    // Ready 버튼에 연결
    public void OnClickReadyButton()
    {
        //var localRoomPlayer = NetworkClient.connection.identity.GetComponent<RoomPlayer>();
        if (_localRoomPlayer != null)
        {
            _localRoomPlayer.CmdChangeReadyState(!_localRoomPlayer.readyToBegin);
        }
    }

    // 로비 나가기 버튼
    public void OnClickLeaveButton()
    {
        if (NetworkServer.active)
        {
            _leaveConfirmPanel.SetActive(true);
        }
        else
        {
            NetworkManager.singleton.StopClient();
        }
    }
    // 호스트가 방 나가기를 할때의 UI에서 확인버튼 클릭시
    public void OnConfirmLeave()
    {
        _leaveConfirmPanel.SetActive(false);
        NetworkManager.singleton.StopHost();
    }

    // 호스트가 방 나가기를 할때의 UI에서 취소버튼 클릭시
    public void OnCancelLeave()
    {
        _leaveConfirmPanel.SetActive(false);
    }

    public void ShowNotification()
    {
        if (_notEnoughPlayerCount == null) return;

        // 이미 실행 중인 연출이 있다면 중단하고 새로 시작
        if (_warningCoroutine != null)
        {
            StopCoroutine(_warningCoroutine);
        }

        _warningCoroutine = StartCoroutine(ShowTextRoutine());
    }
    private IEnumerator ShowTextRoutine()
    {
        _notEnoughPlayerCount.SetActive(true); // UI 활성화

        yield return new WaitForSeconds(_dispalyTime); // 지정한 시간(예: 2초) 동안 대기

        _notEnoughPlayerCount.SetActive(false); // UI 비활성화
        _warningCoroutine = null;
    }
}
