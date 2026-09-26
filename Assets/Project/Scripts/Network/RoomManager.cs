using Mirror;
using ProjectShaman.Steam;
using System;
using System.Linq;
using UnityEngine;
using kcp2k;

public class RoomManager : NetworkRoomManager
{
    [Header("Transport")]
    [SerializeField] private SteamTransport _steamTransport;
    [SerializeField] private KcpTransport _kcpTransport;

    [SerializeField] private bool _useKcpInEditor = true;

    public bool IsUsingSteam => transport is SteamTransport;

    public int SelectedGhostCount { get; private set; }

    private bool _isAttemptingJoin;
    public void BeginJoinAttempt() => _isAttemptingJoin = true;

    // [클라] 접속 시도가 로비 입장 전에 끊겼을 때 발생
    public static event Action JoinFailed;

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

    public void SetGhostCount(int ghostCount)
    {
        SelectedGhostCount = ghostCount;
    }



    public override void OnRoomClientEnter()
    {
        base.OnRoomClientEnter();
        _isAttemptingJoin = false; // 정상적으로 로비에 들어옴
    }
    public override void OnRoomClientDisconnect()
    {
        base.OnRoomClientDisconnect();

        if (_isAttemptingJoin)
        {
            _isAttemptingJoin = false;
            JoinFailed?.Invoke();
        }


        //Debug.Log($"[RoomManager] OnRoomClientDisconnect 호출됨, isAttemptingJoin={_isAttemptingJoin}");
        //if (_isAttemptingJoin)
        //{
        //    _isAttemptingJoin = false;
        //    // 방에 접속 불가 UI호출
        //    var onlineUI = Object.FindFirstObjectByType<OnlineUI>(FindObjectsInactive.Include);
        //    Debug.Log($"[RoomManager] onlineUI 찾음? {onlineUI != null}");
        //    onlineUI?.ShowJoinFailed();
        //}
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
}
