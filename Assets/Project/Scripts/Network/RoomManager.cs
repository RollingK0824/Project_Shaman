using kcp2k;
using Mirror;
using ProjectShaman.Steam;
using Steamworks;
using System;
using System.Linq;
using UnityEngine;


using static RoomManager;

public enum DisconnectNotice
{
    None,
    HostLost,
}

public class RoomManager : NetworkRoomManager
{
    [Header("Transport")]
    [SerializeField] private SteamTransport _steamTransport;
    [SerializeField] private KcpTransport _kcpTransport;

    [Tooltip("메인 메뉴에서 LAN/Steam을 고르기 전의 기본값. 버튼을 누르면 그 선택이 우선합니다.")]
    [SerializeField] private bool _useKcpInEditor = true;

    [Header("Steam 로비")]
    [SerializeField] private SteamLobbyService _steamLobby;

    public static DisconnectNotice PendingNotice { get; private set;  }


    public bool IsUsingSteam => transport is SteamTransport;

    public enum NetworkMode { Lan, Steam}
    public bool IsSteamAvailable => _steamTransport != null && _steamTransport.Available();

    public int SelectedGhostCount { get; private set; }

    // [Steam 로비] 방 생성 화면에서 설정. 비어 있으면 "방장 방"
    public string RoomName { get; set; }
    public string RoomPassword { get; set; }
    public bool HasPassword => !string.IsNullOrEmpty(RoomPassword);

    private bool _isAttemptingJoin;
    public void BeginJoinAttempt() => _isAttemptingJoin = true;

    // 접속 시도가 로비 입장 전에 끊겼을 때 발생
    public static event Action JoinFailed;

    // 게임 도중 참가자가 나갈 때, 그 플레이어 오브젝트가 파괴되기 직전에 발생
    public static event Action <GameObject> ServerGamePlayerLeaving;

    // 서버 종료 중인지. 종료할 때는 모든 연결이 한꺼번에 끊기므로 이탈로 보지 않음
    private bool _isServerStopping;

    // 사용자가 직접 나가기를 눌렀는지. 이 경우에는 끊김 안내를 띄우지 않음
    private bool _isLeavingVoluntarily;




    public override void Awake()
    {
        // base.Awake() 안에서 Transport.active = transport 로 확정되므로 그 전에 선택.
        bool useKcp = Application.isEditor && _useKcpInEditor;
        Transport selected = useKcp ? _kcpTransport : _steamTransport;

        if (selected != null)
        {
            transport = selected;
        }
        else
        {
            Debug.LogError($"[RoomManager] {(useKcp ? "KcpTransport" : "SteamTransport")}가 인스펙터에 연결되지 않았습니다. 기존 transport를 사용합니다.");
        }

        base.Awake();

        Debug.Log($"[RoomManager] Transport: {transport.GetType().Name}");
    }

    public override void OnStartServer()
    {
        _isServerStopping = false;
        base.OnStartServer();
    }

    public override void OnStartClient()
    {
        _isLeavingVoluntarily = false;
        base.OnStartClient();
    }
    // [Steam 로비] Steam으로 호스트를 시작하면 방 목록에 등록
    public override void OnStartHost()
    {
        base.OnStartHost();

        if (IsUsingSteam && _steamLobby != null)
        {
            string owner = GetOwnerName();
            string roomName = string.IsNullOrWhiteSpace(RoomName) ? $"{owner}'s room" : RoomName;
            _steamLobby.HostLobby(roomName, owner, maxConnections, SelectedGhostCount, HasPassword);
        }
    }

    // [Steam 로비] 접속하면 인원 갱신
    public override void OnRoomServerConnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerConnect(conn);
        UpdateLobbyPlayerCount(null);
    }

    // [Steam 로비] 게임 씬으로 가면 "게임 중", 로비 씬으로 돌아오면 "대기 중"
    public override void OnRoomServerSceneChanged(string sceneName)
    {
        base.OnRoomServerSceneChanged(sceneName);
        if (_steamLobby != null) _steamLobby.SetInGame(sceneName == GameplayScene);
    }

    public override void OnStopServer()
    {
        _isServerStopping = true;
        base.OnStopServer();
    }
    
    // [Steam 로비] 호스트 종료 시 방 목록에서 제거
    public override void OnStopHost()
    {
        if (_steamLobby != null) _steamLobby.CloseLobby();
        base.OnStopHost();
    }

    public void SetGhostCount(int ghostCount)
    {
        SelectedGhostCount = ghostCount;
    }



    public override void OnRoomClientEnter()
    {
        base.OnRoomClientEnter();
        _isAttemptingJoin = false; // 정상적으로 로비에 들어옴
    }
    public override void OnRoomServerDisconnect(NetworkConnectionToClient conn)
    {
        base.OnRoomServerDisconnect(conn);
        UpdateLobbyPlayerCount(conn);   // [Steam 로비] 아래 return보다 먼저 (로비 씬 이탈도 반영)

        if (_isServerStopping || conn is LocalConnectionToClient)
        {
            return;
        }

        if (!Mirror.Utils.IsSceneActive(GameplayScene))
        {
            return;
        }

        if (conn.identity == null)
        {
            return;
        }

        Debug.Log($"[RoomManager] 게임 중 이탈: connId = {conn.connectionId}");
        ServerGamePlayerLeaving?.Invoke(conn.identity.gameObject);
    }
    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();

        if (_isAttemptingJoin)
        {
            _isAttemptingJoin = false;
            JoinFailed?.Invoke();
            return;
        }

        if (!_isLeavingVoluntarily && !NetworkServer.active)
        {
            PendingNotice = DisconnectNotice.HostLost;
        }
    }

    public override void OnGUI()
    {
        
    }

    // 전원 Ready 누르면 Mirror가 자동으로 호출 -> 기본 구현이 이미 GameplayScene으로 전환해줌
    // "정확히 몇명이어야 시작" 같은 조건을 추가하고 싶을 때만 override를 사용하자
    public override void OnRoomServerPlayersReady()
    {
        if (roomSlots.Count < minPlayers)
        {
            if (roomSlots.Count > 0)
            {
                (roomSlots.ElementAtOrDefault(0) as RoomPlayer)?.RpcShowNotEnoughPlayers();             
            }
            return;
        }

        base.OnRoomServerPlayersReady();
    }

    // 로비 -> 게임 전환 시 NetPlayer를 생성하고 스폰 전에 닉네임을 설정합니다.
    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        var roomPlayerComp = roomPlayer.GetComponent<RoomPlayer>();

        // 1. 스폰 위치 - 게임 씬의 PlayerSpawnArea가 있으면 로비 번호로 원형 배치, 없으면 기존 방식
        Vector3 spawnPos;
        Quaternion spawnRot;

        PlayerSpawnArea spawnArea = FindFirstObjectByType<PlayerSpawnArea>();
        if (spawnArea != null && roomPlayerComp != null)
        {
            spawnArea.GetSpawnPose(roomPlayerComp.index, maxConnections, out spawnPos, out spawnRot);
        }
        else
        {
            Transform startPos = GetStartPosition();
            spawnPos = startPos != null ? startPos.position : Vector3.zero;
            spawnRot = startPos != null ? startPos.rotation : Quaternion.identity;
        }

        // 2. 게임 플레이어 생성
        GameObject gamePlayerObj = Instantiate(playerPrefab, spawnPos, spawnRot);

        // 3. 로비의 닉네임을 게임 플레이어의 공개 상태로 전달
        var status = gamePlayerObj.GetComponent<NetPlayerStatus>();

        if (roomPlayerComp != null && status != null)
        {
            status.ServerInitialize(roomPlayerComp.nickname);
        }
        else
        {
            Debug.LogError("[RoomManager] 플레이어 프리팹에 NetPlayerStatus가 없거나 RoomPlayer를 찾지 못했습니다.");
        }

        return gamePlayerObj;
    }


    public static DisconnectNotice ConsumeNotice()
    {
        DisconnectNotice notice = PendingNotice;
        PendingNotice = DisconnectNotice.None;
        return notice;
    }

    public void LeaveSession()
    {
        _isLeavingVoluntarily = true;

        if (NetworkServer.active && NetworkClient.isConnected) StopHost();
        else if (NetworkClient.active) StopClient();
        else if (NetworkServer.active) StopServer();
    }


    // 메인 메뉴에서 연결 방식 선택. 접속 중에는 바꿀 수 없음
    public bool TrySelectMode(NetworkMode mode)
    {
        if (NetworkServer.active || NetworkClient.active)
        {
            Debug.LogWarning("[RoomManager] 접속 중에는 연결 방식을 바꿀 수 없습니다.");
            return false;
        }

        Transport selected = mode == NetworkMode.Steam ? _steamTransport : _kcpTransport;
        if (selected == null)
        {
            Debug.LogError($"[RoomManager] {mode}용 Transport가 인스펙터에 연결되지 않았습니다.");
            return false;
        }

        if (!selected.Available()) return false;   // Steam이 꺼져 있거나 초기화 실패

        transport = selected;
        Transport.active = selected;               // Mirror가 실제로 사용하는 값
        Debug.Log($"[RoomManager] Transport 선택: {selected.GetType().Name}");
        return true;
    }


    // Steam 로비 보조 함수들

    private void UpdateLobbyPlayerCount(NetworkConnectionToClient leaving)
    { // leaving : 지금 끊기는 연결. 끊금 콜백 시점에는 목록에 남아 있을 수 있기에 직접 제외
        if (_steamLobby == null) return;

        int count = NetworkServer.connections.Count;
        if (leaving != null && NetworkServer.connections.ContainsKey(leaving.connectionId)) count--;
        _steamLobby.UpdatePlayerCount(count);
    }

    private string GetOwnerName() =>
        string.IsNullOrWhiteSpace(UserData.Nickname) ? SteamFriends.GetPersonaName() : UserData.Nickname;
}
