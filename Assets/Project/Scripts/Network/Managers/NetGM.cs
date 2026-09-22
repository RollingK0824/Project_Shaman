using UnityEngine;
using Mirror;
using Mirror.BouncyCastle.Bcpg.OpenPgp;
using Steamworks;
using UnityEngine.EventSystems;
using Unity.Collections.LowLevel.Unsafe;

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

        var roomManager = NetworkManager.singleton as RoomManager;

        if (roomManager == null)
        {
            Debug.LogError("[NetGM] RoomManager가 없습니다.");
            return;
        }



        ServerInitialize(_initialNpcCount, roomManager.SelectedGhostCount);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        ApplySnapshot(_snapshot);
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

}
