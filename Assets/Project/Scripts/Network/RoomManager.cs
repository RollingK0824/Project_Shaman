using Mirror;
using ProjectShaman.Steam;
using System;
using System.Linq;
using UnityEngine;
using kcp2k;

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

    [SerializeField] private bool _useKcpInEditor = true;

    public static DisconnectNotice PendingNotice { get; private set;  }


    public bool IsUsingSteam => transport is SteamTransport;

    public int SelectedGhostCount { get; private set; }

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

    public override void OnStopServer()
    {
        _isServerStopping = true;
        base.OnStopServer();
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

    // 로비 -> 게임 전환 시 실제 GamePlayer 스폰하는 지점
    public override GameObject OnRoomServerCreateGamePlayer(NetworkConnectionToClient conn, GameObject roomPlayer)
    {
        // 1. 스폰 위치 가져오기 (NetworkStartPosition이 없으면 Vector3.zero 사용)
        Transform startPos = GetStartPosition();
        Vector3 spawnPos = startPos != null ? startPos.position : Vector3.zero;
        Quaternion spawnRot = startPos != null ? startPos.rotation : Quaternion.identity;

        // 2. GamePlayer 생성
        GameObject gamePlayerObj = Instantiate(playerPrefab, spawnPos, spawnRot);

        // 3. 컴포넌트 가져오기 (gameObject가 아니라 gamePlayerObj에서 가져와야 함!)
        var roomPlayerComp = roomPlayer.GetComponent<RoomPlayer>();
        var gamePlayerComp = gamePlayerObj.GetComponent<GamePlayer>();

        // 4. 데이터 복사 (Null 안전성 확인)
        if (roomPlayerComp != null && gamePlayerComp != null)
        {
            gamePlayerComp.playerName = roomPlayerComp.nickname;
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
}
