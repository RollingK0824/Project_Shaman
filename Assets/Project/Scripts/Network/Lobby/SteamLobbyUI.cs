using Mirror;
using Steamworks;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Steam 방 목록 화면: 새로고침, 검색, 필터, 선택한 방 정보, 생성, 참가
public class SteamLobbyUI : MonoBehaviour
{
    // 드롭다운 옵션 순서와 같아야 함 (전체 방 / 참가 가능 / 게임 중)
    private enum RoomFilter { All = 0, Joinable = 1, InGame = 2 }

    [Header("목록")]
    [SerializeField] private RoomRowUI _rowPrefab;
    [SerializeField] private Transform _listContent;
    [SerializeField] private TMP_Text _emptyText;
    [SerializeField] private TMP_InputField _searchInput;
    [SerializeField] private TMP_Dropdown _filterDropdown;
    [SerializeField] private Button _refreshButton;

    [Header("선택한 방 정보")]
    [SerializeField] private TMP_Text _infoNameText;
    [SerializeField] private TMP_Text _infoOwnerText;
    [SerializeField] private TMP_Text _infoPlayersText;
    [SerializeField] private TMP_Text _infoStateText;
    [SerializeField] private Button _joinButton;
    [SerializeField] private TMP_Text _messageText;       // 참가 실패 등 안내 (없어도 됨)

    [Header("화면 이동")]
    [SerializeField] private GameObject _mainMenuUI;
    [SerializeField] private GameObject _createRoomUI;

    [Header("비밀번호 입력창")]
    [SerializeField] private GameObject _passwordPanel;
    [SerializeField] private TMP_InputField _joinPasswordInput;


    private readonly List<RoomInfo> _rooms = new List<RoomInfo>();
    private readonly List<RoomRowUI> _rows = new List<RoomRowUI>();
    private string _selectedAddress;      // 새로고침 후에도 같은 방을 다시 선택하기 위해 주소로 기억
    private bool _isLoading;
    private string _pendingJoinAddress;

    // RoomManager는 DontDestroyOnLoad라서 인스펙터로 연결하면 메인 메뉴에 돌아왔을 때 끊어짐 → 매번 찾음
    private static SteamLobbyService LobbyService =>
        RoomManager.singleton != null ? RoomManager.singleton.GetComponent<SteamLobbyService>() : null;

    private void Awake()
    {
        _searchInput.onValueChanged.AddListener(_ => RebuildList());
        _filterDropdown.onValueChanged.AddListener(_ => RebuildList());
    }

    private void OnEnable()
    {
        RoomManager.JoinFailed += OnJoinFailed;
        ShowMessage(string.Empty);
        Refresh();

        if (_passwordPanel != null) _passwordPanel.SetActive(false);
    }

    private void OnDisable()
    {
        RoomManager.JoinFailed -= OnJoinFailed;
    }

    // ───────── 목록 ─────────

    public void Refresh()
    {
        SteamLobbyService service = LobbyService;
        if (service == null)
        {
            ShowMessage("SteamLobbyService not found.");
            return;
        }

        _isLoading = true;
        _refreshButton.interactable = false;
        UpdateEmptyText();

        service.RequestRooms(OnRoomsReceived);
    }

    private void OnRoomsReceived(IReadOnlyList<RoomInfo> rooms)
    {
        if (this == null) return;   // 응답을 기다리는 사이 씬이 바뀌어 UI가 파괴된 경우

        _isLoading = false;
        _refreshButton.interactable = true;

        _rooms.Clear();
        _rooms.AddRange(rooms);
        RebuildList();
    }

    // 검색어·필터가 바뀌면 다시 요청하지 않고 받아 둔 목록만 다시 그림
    private void RebuildList()
    {
        foreach (RoomRowUI row in _rows) Destroy(row.gameObject);
        _rows.Clear();

        RoomInfo? selected = null;
        foreach (RoomInfo room in _rooms)
        {
            if (!PassesFilter(room)) continue;

            RoomRowUI row = Instantiate(_rowPrefab, _listContent);
            row.Bind(room, OnRowClicked);
            _rows.Add(row);

            if (room.Address == _selectedAddress)
            {
                row.SetSelected(true);
                selected = room;
            }
        }

        // 선택했던 방이 사라졌거나 필터에 걸리면 선택 해제
        if (!selected.HasValue) _selectedAddress = null;

        ShowRoomInfo(selected);
        UpdateEmptyText();
    }

    private bool PassesFilter(RoomInfo room)
    {
        string keyword = _searchInput.text.Trim();
        if (keyword.Length > 0 &&
            (room.Name ?? string.Empty).IndexOf(keyword, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return false;
        }

        switch ((RoomFilter)_filterDropdown.value)
        {
            case RoomFilter.Joinable: return room.IsJoinable;
            case RoomFilter.InGame: return !room.IsWaiting;
            default: return true;
        }
    }

    private void UpdateEmptyText()
    {
        bool show = _isLoading || _rows.Count == 0;
        _emptyText.gameObject.SetActive(show);
        if (!show) return;

        if (_isLoading) _emptyText.text = "불러오는 중...";
        else if (_rooms.Count == 0) _emptyText.text = "방이 없습니다";
        else _emptyText.text = "조건에 맞는 방이 없습니다";
    }

    // ───────── 선택 ─────────

    private void OnRowClicked(RoomRowUI clicked)
    {
        _selectedAddress = clicked.Room.Address;
        foreach (RoomRowUI row in _rows) row.SetSelected(row == clicked);
        ShowRoomInfo(clicked.Room);
    }

    private void ShowRoomInfo(RoomInfo? room)
    {
        bool has = room.HasValue;

        _infoNameText.text = has ? room.Value.Name : "-";
        _infoOwnerText.text = has ? room.Value.Owner : "-";
        _infoPlayersText.text = has ? $"{room.Value.CurrentPlayers} / {room.Value.MaxPlayers}" : "-";
        _infoStateText.text = has ? $"{(room.Value.IsLocked ? "비공개 방" : "공개 방")} · {RoomRowUI.GetStateLabel(room.Value)}" : "-";

        _joinButton.interactable = has && room.Value.IsJoinable;
    }

    private RoomInfo? FindSelectedRoom()
    {
        foreach (RoomInfo room in _rooms)
        {
            if (room.Address == _selectedAddress) return room;
        }
        return null;
    }

    // ───────── 버튼 ─────────

    public void OnClickJoin()
    {
        if (NetworkClient.active || NetworkServer.active) return;

        RoomInfo? room = FindSelectedRoom();
        if (!room.HasValue || !room.Value.IsJoinable) return;

        // 비밀번호 방이면 입력창을 먼저 띄움
        if (room.Value.IsLocked && _passwordPanel != null)
        {
            _pendingJoinAddress = room.Value.Address;
            _joinPasswordInput.text = string.Empty;
            _passwordPanel.SetActive(true);
            _joinPasswordInput.ActivateInputField();
            return;
        }

        StartJoin(room.Value.Address, string.Empty);

        //var manager = RoomManager.singleton as RoomManager;
        //if (manager == null) return;
        //
        //// Steam 화면에는 닉네임 입력칸이 없으므로 Steam 이름 사용
        //UserData.Nickname = SteamFriends.GetPersonaName();
        //
        //_joinButton.interactable = false;   // 중복 클릭 방지
        //ShowMessage("Joining...");
        //
        //manager.networkAddress = room.Value.Address;
        //manager.BeginJoinAttempt();
        //manager.StartClient();
    }

    private void OnJoinFailed()
    {
        // 비밀번호 거절이면 그 사유, 아니면 일반 실패 (방이 사라졌거나 가득 참)
        ShowMessage(RoomPasswordAuthenticator.ConsumeRejectReason() ?? "Failed to join the room.");
        Refresh();
    }

    public void OnClickCreate()
    {
        UserData.Nickname = SteamFriends.GetPersonaName();
        _createRoomUI.SetActive(true);
        gameObject.SetActive(false);
    }

    public void OnClickBack()
    {
        _mainMenuUI.SetActive(true);
        gameObject.SetActive(false);
    }

    public void OnClickPasswordConfirm()
    {
        if (string.IsNullOrEmpty(_pendingJoinAddress)) return;

        _passwordPanel.SetActive(false);
        StartJoin(_pendingJoinAddress, _joinPasswordInput.text);
        _pendingJoinAddress = null;
    }

    // 비밀번호 입력창 [취소]
    public void OnClickPasswordCancel()
    {
        _pendingJoinAddress = null;
        _passwordPanel.SetActive(false);
    }

    private void StartJoin(string address, string password)
    {
        var manager = RoomManager.singleton as RoomManager;
        if (manager == null) return;

        // Steam 화면에는 닉네임 입력칸이 없으므로 Steam 이름 사용
        UserData.Nickname = SteamFriends.GetPersonaName();
        RoomPasswordAuthenticator.PendingPassword = password;

        _joinButton.interactable = false;   // 중복 클릭 방지
        ShowMessage("Joining...");

        manager.networkAddress = address;
        manager.BeginJoinAttempt();
        manager.StartClient();
    }

    private void ShowMessage(string message)
    {
        if (_messageText != null) _messageText.text = message;
    }
}