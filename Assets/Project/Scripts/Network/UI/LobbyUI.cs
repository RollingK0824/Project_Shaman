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

    [SerializeField] private TMP_Text _readyButtonText;
    [SerializeField] private Button _readyButton;
    [SerializeField] private TMP_Text _playerCountText;

    [SerializeField] private GameObject _notEnoughPlayerCount;
    [SerializeField] private float _dispalyTime = 2f;

    [SerializeField] private Button _inviteButton;

    [SerializeField] private FriendInvitePanel _friendInvitePanel;

    [Header("캐릭터 선택")]
    [SerializeField] private Button[] _characterButtons; // NetPlayer1~4

    [SerializeField] private LobbyCharacterPreview _characterPreview;

    [Header("인게임 이름")]
    [SerializeField] private TMP_InputField _gameNameInput;
    [SerializeField] private TMP_Text _gameNameHintText;       // 입력칸 아래 상태 문구
    [SerializeField] private GameObject _nameRequiredWarning;  // 이름 없이 준비를 눌렀을 때 잠깐 표시

    private Coroutine _nameWarningCoroutine;


    private Coroutine _warningCoroutine;
    private RoomPlayer _localRoomPlayer;
    private bool _refreshRequested;
    void Start()
    {
        _notEnoughPlayerCount.SetActive(false);
        if (_nameRequiredWarning != null) _nameRequiredWarning.SetActive(false);

        _gameNameInput.characterLimit = PlayerNameRule.MaxLength;
        _gameNameInput.text = string.Empty;
        _gameNameInput.onValueChanged.AddListener(_ => UpdateNameHint());
        _gameNameInput.onEndEdit.AddListener(SubmitGameName);
        UpdateNameHint();
    }

    private void OnEnable()
    {
        // Canvas Lobby는 프리팹이라 씬의 촬영장을 직접 연결할 수 없어 씬에서 찾음
        if (_characterPreview == null) _characterPreview = FindFirstObjectByType<LobbyCharacterPreview>();

        RoomPlayer.NotEnoughPlayers += ShowNotification;
        RoomPlayer.LobbyChanged += RequestRefresh;
        RequestRefresh();

        bool canInvite = NetworkServer.active && NetworkManager.singleton is RoomManager room && room.IsUsingSteam;
        if (_inviteButton != null) _inviteButton.gameObject.SetActive(canInvite);
    }

    private void OnDisable()
    {
        RoomPlayer.NotEnoughPlayers -= ShowNotification;
        RoomPlayer.LobbyChanged -= RequestRefresh;
        _refreshRequested = false;
        _localRoomPlayer = null;
    }


    private void RequestRefresh()
    {
        _refreshRequested = true;
    }

    private void LateUpdate()
    {
        if (!_refreshRequested) return;

        // 같은 프레임의 입장 및 SyncVar 이벤트를 모아 한 번만 갱신합니다.
        // Mirror의 목록 등록과 로컬 플레이어 초기화가 끝난 상태를 읽습니다.
        _refreshRequested = false;
        RefreshUI();
    }

    public void RefreshUI()
    {
        // Destroy는 프레임 끝에 실행되므로 기존 행은 먼저 숨깁니다.
        for (int i = _playerListParent.childCount - 1; i >= 0; i--)
        {
            GameObject row = _playerListParent.GetChild(i).gameObject;
            row.SetActive(false);
            Destroy(row);
        }

        // 2. roomSlots 순회하며 생성
        var roomManager = NetworkManager.singleton as NetworkRoomManager;
        if (roomManager == null)
        {
            _localRoomPlayer = null;
            _readyButton.interactable = false;
            _playerCountText.text = "0 / 0";
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

            string shownName = rp.HasGameName ? rp.gameName : "이름 입력 중";
            row.GetComponent<LobbyPlayerRowUI>().SetPlayerInfo(shownName, rp.readyToBegin, rp.index == 0, rp.colorId);

        }

        _localRoomPlayer = roomManager.roomSlots.OfType<RoomPlayer>().FirstOrDefault(player => player != null && player.isLocalPlayer);

        UpdateCharacterButtons();
        UpdateReadyButton(roomManager);
    }
    private void UpdateReadyButton(NetworkRoomManager roomManager)
    {
        if (_localRoomPlayer == null)
        {
            _readyButton.interactable = false;
            _playerCountText.text = $"{roomManager.roomSlots.Count} / -";
            return;
        }

        _playerCountText.text = $"{roomManager.roomSlots.Count} / {_localRoomPlayer.roomMaxPlayers}";

        if (NetworkServer.active)
        {
            _readyButtonText.text = "시작";

            bool enoughPlayers = roomManager.roomSlots.Count >= roomManager.minPlayers;
            bool othersReady = roomManager.roomSlots.OfType<RoomPlayer>().Where(rp => rp != _localRoomPlayer).All(rp => rp.readyToBegin);

            _readyButton.interactable = enoughPlayers && othersReady;
        }
        else
        {
            _readyButtonText.text = _localRoomPlayer.readyToBegin ? "취소" : "준비";
            _readyButton.interactable = true;
        }
    }

    private void UpdateCharacterButtons()
    {
        for (int i = 0; i < _characterButtons.Length; i++)
        {
            bool canPick = _localRoomPlayer != null
                && !_localRoomPlayer.readyToBegin
                && _localRoomPlayer.characterId != i;

            _characterButtons[i].interactable = canPick;
        }

        if (_characterPreview != null)
        {
            _characterPreview.Show(_localRoomPlayer != null ? _localRoomPlayer.characterId : -1);
        }

        _gameNameInput.interactable = _localRoomPlayer != null && !_localRoomPlayer.readyToBegin;
    }

    // 캐릭터 버튼 OnClick 연결
    public void OnClickCharacter(int id)
    {
        if (_localRoomPlayer != null) _localRoomPlayer.CmdSelectCharacter(id);
    }

    // Ready 버튼에 연결
    // Ready 버튼에 연결 (호스트의 "시작"도 이 함수)
    public void OnClickReadyButton()
    {
        if (_localRoomPlayer == null) return;

        bool wantReady = !_localRoomPlayer.readyToBegin;
        if (wantReady)
        {
            string name = _gameNameInput.text.Trim();
            if (!PlayerNameRule.IsValid(name))
            {
                ShowNameWarning();
                _gameNameInput.Select();
                return;
            }

            // 준비 직전에 이름을 확정. Command는 보낸 순서대로 서버에 도착하므로 이름이 먼저 반영됨
            SubmitGameName(name);
        }

        _localRoomPlayer.CmdChangeReadyState(wantReady);
    }

    // 친구 초대 버튼에 연결
    public void OnClickInviteButton()
    {
        if (_friendInvitePanel == null)
        {
            Debug.LogWarning("[LobbyUI] FriendInvitePanel이 연결되지 않았습니다.");
            return;
        }

        _friendInvitePanel.Open();
    }

    public void OnClickBoardBack()
    {
        if (LobbyBoardFocus.Current != null) LobbyBoardFocus.Current.Unfocus();
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

    private void SubmitGameName(string text)
    {
        string name = text.Trim();
        if (_localRoomPlayer == null || !PlayerNameRule.IsValid(name)) return;

        if (_localRoomPlayer.gameName != name) _localRoomPlayer.CmdSetGameName(name);
    }

    private void UpdateNameHint()
    {
        if (_gameNameHintText == null) return;

        string name = _gameNameInput.text.Trim();
        if (PlayerNameRule.IsValid(name)) _gameNameHintText.text = "";
        else if (name.Length == 0) _gameNameHintText.text = "인게임에서 사용할 이름 (한글 2~4자)";
        else _gameNameHintText.text = "<color=#B22222>한글 2~4글자로 입력하세요</color>";
    }

    private void ShowNameWarning()
    {
        if (_nameRequiredWarning == null) return;
        if (_nameWarningCoroutine != null) StopCoroutine(_nameWarningCoroutine);
        _nameWarningCoroutine = StartCoroutine(NameWarningRoutine());
    }

    private IEnumerator NameWarningRoutine()
    {
        _nameRequiredWarning.SetActive(true);
        yield return new WaitForSeconds(_dispalyTime);
        _nameRequiredWarning.SetActive(false);
        _nameWarningCoroutine = null;
    }
}
