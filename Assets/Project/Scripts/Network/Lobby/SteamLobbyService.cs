using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;



// Steam 로비를 "방 게시판"으로 사용. 실제 연결은 Mirror + SteamTransport가 담당
// 호스트만 로비로 들어가고, 클라는 목록을 읽기만 함 ?
// RoomManager와 같은 오브젝트에 붙임 (DontDestroyOnLoad로 씬이 바뀌어도 유지되도록)
public class SteamLobbyService : MonoBehaviour
{
    // App ID 480은 전세계 개발자가 같이 사용, 이 값으로 우리 게임의 로비만 걸러냄
    private const string GameKey = "game";
    private const string GameValue = "ProjectShaman";

    private const string HostKey = "host";
    private const string NameKey = "name";
    private const string OwnerKey = "owner";
    private const string PlayersKey = "players";
    private const string MaxKey = "max";
    private const string GhostKey = "ghost";
    private const string StateKey = "state";
    private const string StateLobby = "lobby";
    private const string StateInGame = "ingame";
    private const string LockedKey = "locked";


    [SerializeField] private int _maxResults = 50;

    private CallResult<LobbyCreated_t> _lobbyCreatedResult;
    private CallResult<LobbyMatchList_t> _lobbyListResult;
    private Action<IReadOnlyList<RoomInfo>> _pendingListCallback;

    private CSteamID _currentLobby = CSteamID.Nil;
    private bool _isHosting;

    // 로비 생성 응답 전에 바뀐 값도 잃지 않도록 항상 여기에 보관하고, 로비가 있으면 기록
    private string _roomName;
    private string _ownerName;
    private int _maxPlayers;
    private int _ghostCount;
    private int _playerCount;
    private bool _inGame;
    private bool _isLocked;

    private void EnsureCallResults()
    {
        if (_lobbyCreatedResult == null)
        {
            _lobbyCreatedResult = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
        }
        if (_lobbyListResult == null)
        {
            _lobbyListResult = CallResult<LobbyMatchList_t>.Create(OnLobbyList);
        }
    }


    // 호스트 부분
    public void HostLobby(string roomName, string ownerName, int maxPlayers, int ghostCount, bool isLocked)
    {
        if (!SteamManager.Initialized) return;
        EnsureCallResults();

        _isHosting = true;
        _ownerName = ownerName;
        _roomName = roomName;
        _maxPlayers = maxPlayers;
        _ghostCount = ghostCount;
        _playerCount = 1;
        _inGame = false;
        _isLocked = isLocked;

        SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, maxPlayers);
        _lobbyCreatedResult.Set(call);
    }

    private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
    {
        if (ioFailure || result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogWarning($"[SteamLobby] 로비 생성 실패: {result.m_eResult}");
            return;
        }

        var lobby = new CSteamID(result.m_ulSteamIDLobby);

        // 응답을 기다리는 동안 호스트가 종료된 경우 → 유령 방이 남지 않도록 바로 나감
        if (!_isHosting)
        {
            SteamMatchmaking.LeaveLobby(lobby);
            return;
        }

        _currentLobby = lobby;
        SteamMatchmaking.SetLobbyData(lobby, GameKey, GameValue);
        SteamMatchmaking.SetLobbyData(lobby, HostKey, SteamUser.GetSteamID().m_SteamID.ToString(CultureInfo.InvariantCulture));
        SteamMatchmaking.SetLobbyData(lobby, NameKey, _roomName);
        SteamMatchmaking.SetLobbyData(lobby, OwnerKey, _ownerName);
        SteamMatchmaking.SetLobbyData(lobby, MaxKey, _maxPlayers.ToString(CultureInfo.InvariantCulture));
        SteamMatchmaking.SetLobbyData(lobby, GhostKey, _ghostCount.ToString(CultureInfo.InvariantCulture));
        SteamMatchmaking.SetLobbyData(lobby, LockedKey, _isLocked ? "1" : "0");
        PublishState();

        Debug.Log($"[SteamLobby] 로비 생성: {lobby.m_SteamID} ({_roomName} / {_ownerName})");
    }

    public void UpdatePlayerCount(int count)
    {
        _playerCount = count;
        PublishState();
    }

    public void SetInGame(bool inGame)
    {
        _inGame = inGame;
        PublishState();
    }

    // 자주 바뀌는 값(인원, 상태)만 다시 기록
    private void PublishState()
    {
        if (!_currentLobby.IsValid()) return;

        SteamMatchmaking.SetLobbyData(_currentLobby, PlayersKey, _playerCount.ToString(CultureInfo.InvariantCulture));
        SteamMatchmaking.SetLobbyData(_currentLobby, StateKey, _inGame ? StateInGame : StateLobby);
    }

    public void CloseLobby()
    {
        _isHosting = false;
        if (!_currentLobby.IsValid()) return;

        // 마지막 멤버(호스트)가 나가면 Steam이 로비를 삭제
        SteamMatchmaking.LeaveLobby(_currentLobby);
        Debug.Log($"[SteamLobby] 로비 닫기: {_currentLobby.m_SteamID}");
        _currentLobby = CSteamID.Nil;
    }

    // SteamManager 안내대로 Steam 호출은 OnDestroy가 아닌 OnDisable에서
    private void OnDisable()
    {
        if (SteamManager.Initialized) CloseLobby();
    }


    // 클라 부분


    public void RequestRooms(Action<IReadOnlyList<RoomInfo>> onCompleted)
    {
        if (!SteamManager.Initialized)
        {
            onCompleted?.Invoke(Array.Empty<RoomInfo>());
            return;
        }
        EnsureCallResults();

        // 새로고침을 연달아 누르면 마지막 요청에만 응답 (CallResult.Set이 이전 요청을 취소함)
        _pendingListCallback = onCompleted;

        SteamMatchmaking.AddRequestLobbyListStringFilter(GameKey, GameValue, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListResultCountFilter(_maxResults);
        _lobbyListResult.Set(SteamMatchmaking.RequestLobbyList());
    }

    private void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
    {
        var rooms = new List<RoomInfo>();

        if (!ioFailure)
        {
            for (int i = 0; i < result.m_nLobbiesMatching; i++)
            {
                CSteamID lobby = SteamMatchmaking.GetLobbyByIndex(i);
                if (TryReadRoom(lobby, out RoomInfo room)) rooms.Add(room);
            }
        }

        Action<IReadOnlyList<RoomInfo>> callback = _pendingListCallback;
        _pendingListCallback = null;
        callback?.Invoke(rooms);
    }

    private static bool TryReadRoom(CSteamID lobby, out RoomInfo room)
    {
        room = default;

        // 생성 직후라 아직 데이터가 기록되지 않은 로비는 건너뜀
        string host = SteamMatchmaking.GetLobbyData(lobby, HostKey);
        if (string.IsNullOrEmpty(host)) return false;

        string name = SteamMatchmaking.GetLobbyData(lobby, NameKey);
        string owner = SteamMatchmaking.GetLobbyData(lobby, OwnerKey);
        int players = ReadInt(lobby, PlayersKey);
        int max = ReadInt(lobby, MaxKey);
        int ghost = ReadInt(lobby, GhostKey);
        bool waiting = SteamMatchmaking.GetLobbyData(lobby, StateKey) == StateLobby;
        bool locked = SteamMatchmaking.GetLobbyData(lobby, LockedKey) == "1";

        room = new RoomInfo(name, owner, host, players, max, ghost, waiting, locked, waiting && players < max);
        return true;
    }

    private static int ReadInt(CSteamID lobby, string key)
    {
        int.TryParse(SteamMatchmaking.GetLobbyData(lobby, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value);
        return value;
    }
}
