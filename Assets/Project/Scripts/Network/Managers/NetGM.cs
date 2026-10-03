using UnityEngine;
using Mirror;
using System.Collections;

/* *GameManager 동기화할 데이트들
* NpcTotal : 초기 NPC 수 동기화
* NpcCount : 현재 생존 NPC 수 동기화
* GhostCount : 귀신 수 (플레이어에게 공개할 정보면 동기화)
* CurrentGameState : Ongoing / Win / Lose 동기화
* IsGameOver : 별도로 보내지 않고 CurrentGameState로 계산
*/

public struct GameStateSnapshot
{
    public bool initialized;
    public int npcTotal;
    public int npcCount;
    public int ghostCount;
    public GameState state;
}

[RequireComponent(typeof(NetworkIdentity))]
public class NetGM : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnGMStatusChanged))]
    private GameStateSnapshot _snapshot;

    [Header("초기 개체 수")]
    [SerializeField, Min(1)] private int _initialNpcCount = 20;

    [Header("게임 종료")]
    [SerializeField, Min(0f)] private float _returnToLobbyDelay = 5f;

    [SyncVar] private double _returnToLobbyTime;

    public double ReturnToLobbyTime => _returnToLobbyTime;

    private GameManager _serverGameManager;

    // [서버] 이번 프레임에 참가자, 생존자가 바뀌었는지 LateUpdate에서 한번만 검사
    private bool _rosterDirty;

    // 게임 준비가 끝난 시점에 서버에서 호출
    [Server]
    public void ServerInitialize(int totalNpc, int totalGhost)
    {
        if (_snapshot.initialized)
        {
            return;
        }

        if (totalNpc <= 0 || totalGhost <= 0)
        {
            Debug.LogError("[NetGM] 초기 NPC 수와 귀신 수는 1 이상이어야 합니다.");
            return;
        }

        GameManager.Instance.Init(totalNpc, totalGhost);
        ServerPublishState();
    }

    // 서버에서 NPC 사망이 확정됬을 때 한 번 호출
    [Server]
    public void ServerReportNpcDied()
    {
        if (!_snapshot.initialized)
        {
            return;
        }

        var gm = GameManager.Instance;

        if (gm.IsGameOver || gm.NpcCount <= 0)
        {
            return;
        }

        gm.DecrementNpcCount();
        ServerPublishState();
    }

    // 서버에서 귀신 퇴마가 획정됬을 때 한 번 호출
    [Server]
    public void ServerReportGhostExorcised()
    {
        if (!_snapshot.initialized)
        {
            return;
        }

        var gm = GameManager.Instance;

        if (gm.IsGameOver || gm.GhostCount <= 0)
        {
            return;
        }

        gm.DecrementGhostCount();
        ServerPublishState();
    }


    [Server]
    private void ServerPublishState()
    {
        var gm = GameManager.Instance;

        _snapshot = new GameStateSnapshot
        {
            initialized = true,
            npcTotal = gm.NpcTotal,
            npcCount = gm.NpcCount,
            ghostCount = gm.GhostCount,
            state = gm.CurrentGameState
        };
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        PlayerRoster.Changed += HandleRosterChanged;

        _serverGameManager = GameManager.Instance;
        _serverGameManager.OnWin += HandleGameOverOnServer;
        _serverGameManager.OnLose += HandleGameOverOnServer;

        var roomManager = NetworkManager.singleton as RoomManager;

        if (roomManager == null)
        {
            Debug.LogError("[NetGM] RoomManager가 없습니다.");
            return;
        }


        ServerInitialize(_initialNpcCount, roomManager.SelectedGhostCount);
    }

    public override void OnStopServer()
    {
        PlayerRoster.Changed -= HandleRosterChanged;

        if (_serverGameManager != null)
        {
            _serverGameManager.OnWin -= HandleGameOverOnServer;
            _serverGameManager.OnLose -= HandleGameOverOnServer;
        }

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplySnapshot(_snapshot);
    }

    private void LateUpdate()
    {
        if (!_rosterDirty)
        {
            return;
        }

        _rosterDirty = false;
        ServerCheckAllPlayersDead();
    }

    [Server]
    private void ServerCheckAllPlayersDead()
    {
        if (!_snapshot.initialized) return;

        var gm = GameManager.Instance;
        if (gm.IsGameOver) return;

        if (PlayerRoster.Players.Count == 0) return;
        if (PlayerRoster.AliveCount > 0) return;

        Debug.Log("[NetGM] 생존 플레이어 0명 -> 패배");
        gm.SetGameState(GameState.Lose);
        ServerPublishState();
    }


    private void OnGMStatusChanged(GameStateSnapshot oldValue, GameStateSnapshot newValue)
    {
        ApplySnapshot(newValue);
    }

    private void ApplySnapshot(GameStateSnapshot snapshot)
    {
        if (isServer || !snapshot.initialized)
        {
            return;
        }

        GameManager.Instance.ApplyNetworkState(
            snapshot.npcTotal,
            snapshot.npcCount,
            snapshot.ghostCount,
            snapshot.state);
    }


    private void HandleRosterChanged()
    {
        _rosterDirty = true;
    }


    [Server]
    private void HandleGameOverOnServer()
    {
        if (_returnToLobbyTime > 0) return;

        _returnToLobbyTime = NetworkTime.time + _returnToLobbyDelay;

        Debug.Log($"[NetGM] 게임 종료: {GameManager.Instance.CurrentGameState} -> {_returnToLobbyDelay:0}초 후 로비 복귀");

        StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        yield return new WaitForSeconds(_returnToLobbyDelay);

        if (!NetworkServer.active) yield break;

        if (NetworkManager.singleton is RoomManager roomManager)
        {
            roomManager.ServerChangeScene(roomManager.RoomScene);
        }
    }
}
