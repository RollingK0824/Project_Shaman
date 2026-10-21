using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 시작 게이트 (게임 씬의 네트워크 씬 오브젝트)
// [서버] 로비 참가자(roomSlots) 전원의 준비 + NPC 초기화를 확인한 뒤 공통 시작 시각을 정함
// [서버·클라] 시작 시각이 되면 Started 발생. 클라는 RoomManager에 알려 로딩 화면을 닫고 입력을 허용
[RequireComponent(typeof(NetworkIdentity))]
public class NetGameStartGate : NetworkBehaviour
{
    [SerializeField, Min(1f)] private float _readyTimeout = 20f;
    [SerializeField, Min(0f)] private float _startDelay = 1f;    // 시작 시각 SyncVar가 모든 클라에 도착할 여유

    [SyncVar] private double _startTime;    // 0이면 아직 미정
    // [서버 -> 모든 클라] 로딩 화면 표시용 준비 인원
    [SyncVar] private int _readyCount;
    [SyncVar] private int _expectedCount;


    public static NetGameStartGate Instance { get; private set; }

    // 게이트가 없는 씬에서는 항상 true
    public static bool HasStarted => Instance == null || Instance._started;

    // 시작 시각에 한 번 발생 (서버·클라 각각)
    public static event Action Started;

    public int ReadyCount => _readyCount;
    public int ExpectedCount => _expectedCount;
    public bool IsStartScheduled => _startTime > 0;

    public double StartTime => _startTime;

    // 이미 시작했으면 바로 실행, 아니면 시작할 때 한 번 실행
    public static void WhenStarted(Action action)
    {
        if (HasStarted)
        {
            action();
            return;
        }

        void Once()
        {
            Started -= Once;
            action();
        }
        Started += Once;
    }

    private bool _started;

    // [서버]
    private readonly HashSet<NetworkConnectionToClient> _expected = new HashSet<NetworkConnectionToClient>();
    private readonly HashSet<NetworkConnectionToClient> _ready = new HashSet<NetworkConnectionToClient>();
    private NetGM _netGM;
    private double _deadline;
    private bool _decided;  // 시작 또는 취소가 결정됨 (이후 판정은 모두 무시)


    // 서버는 SpawnObjects에서 씬 오브젝트를 전부 활성화한 뒤 OnStartServer를 부르기에 AI 등의 OnStartServer보다 항상 먼저 Instance가 등록됨
    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        Instance = null;
        Started = null;     // 취소로 씬이 닫혀 발생하지 못한 구독이 다음 판으로 넘어가지 않도록
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _deadline = NetworkTime.time + _readyTimeout;

        // 기대 목록 = 실제 로비 참가자
        // 로딩 중에 끊긴 참가자는 Mirror가 이미 RoomSlots에서 제거
        if (NetworkManager.singleton is RoomManager roomManager)
        {
            foreach (NetworkRoomPlayer slot in roomManager.roomSlots)
            {
                if (slot != null && slot.connectionToClient != null) _expected.Add(slot.connectionToClient);
            }
        }

        RoomManager.ServerConnectionLeft += ServerHandleConnectionLeft;

        _netGM = FindFirstObjectByType<NetGM>();
        if (_netGM != null) _netGM.ServerInitStateChanged += ServerHandleInitStateChanged;

        // 구독 직후 현재 상태 확인 (NetGM 초기화가 이미 끝났을 수 있음)
        ServerCheck();
    }

    public override void OnStopServer()
    {
        RoomManager.ServerConnectionLeft -= ServerHandleConnectionLeft;
        if (_netGM != null) _netGM.ServerInitStateChanged -= ServerHandleInitStateChanged;
        base.OnStopServer();
    }

    // [서버] NetPlayer.CmdReportGameReady에서 호출.기대 목록에 있는 연결만 인정
    [Server]
    public void ServerMarkReady(NetworkConnectionToClient conn)
    {
        if (_decided || !_expected.Contains(conn)) return;

        _ready.Add(conn);
        ServerCheck();
    }

    private void ServerHandleConnectionLeft(NetworkConnectionToClient conn)
    {
        _expected.Remove(conn);
        _ready.Remove(conn);
        ServerCheck();
    }

    private void ServerHandleInitStateChanged(NetInitState state)
    {
        ServerCheck();
    }

    [Server]
    private void ServerCheck()
    {
        _readyCount = _ready.Count;
        _expectedCount = _expected.Count;

        if (_decided) return;

        NetInitState init = _netGM != null ? _netGM.ServerInitState : NetInitState.Ready;
        if (init == NetInitState.Failed)
        {
            ServerCancel("NPC 초기화 실패");
            return;
        }

        if (init != NetInitState.Ready) return;
        if (!_ready.IsSupersetOf(_expected)) return;

        ServerStart();
    }

    [Server]
    private void ServerUpdateTimeout()
    {
        if (_decided || NetworkTime.time < _deadline) return;

        NetInitState init = _netGM != null ? _netGM.ServerInitState : NetInitState.Ready;
        if (init != NetInitState.Ready)
        {
            ServerCancel("NPC 초기화 시간 초과");
            return;
        }

        List<NetworkConnectionToClient> notReady = new List<NetworkConnectionToClient>();
        foreach (NetworkConnectionToClient conn in _expected)
        {
            if (_ready.Contains(conn)) continue;

            // 호스트는 제외할 수 없으므로 전체 취소
            if (conn is LocalConnectionToClient)
            {
                ServerCancel("호스트 준비 시간 초과");
                return;
            }
            notReady.Add(conn);
        }

        // 미준비 클라는 연결 종료 후 나머지로 시작
        // 끊김 콜백은 다음 프레임 이후에 올 수 있으므로 목록에서 먼저 빼서 중복 처리를 막음
        foreach (NetworkConnectionToClient conn in notReady)
        {
            Debug.LogWarning($"[NetGameStartGate] 준비 시간 초과로 연결 종료: connId = {conn.connectionId}");
            _expected.Remove(conn);
            conn.Disconnect();
        }

        ServerCheck();
    }

    [Server]
    private void ServerStart()
    {
        _decided = true;
        _startTime = NetworkTime.time + _startDelay;

        NetTimeManager time = FindFirstObjectByType<NetTimeManager>();
        if (time != null) time.ServerSetCycleStart(_startTime);

        Debug.Log($"[NetGameStartGate] 전원 준비 완료 ({_ready.Count}명) → {_startDelay:0.0}초 후 시작");
    }

    [Server]
    private void ServerCancel(string reason)
    {
        _decided = true;
        Debug.LogWarning($"[NetGameStartGate] 시작 취소: {reason} → 로비 복귀");

        if (NetworkManager.singleton is RoomManager roomManager)
        {
            roomManager.ServerChangeScene(roomManager.RoomScene);
        }
    }

    private void Update()
    {
        if (isServer) ServerUpdateTimeout();

        if (_started || _startTime <= 0 || NetworkTime.time < _startTime) return;

        _started = true;
        Started?.Invoke();

        // [클라] 로딩 화면 닫기 + 로컬 입력 허용 (호스트 포함)
        if (isClient && NetworkManager.singleton is RoomManager roomManager)
        {
            roomManager.ClientNotifyGameStarted();
        }
    }
}
