using UnityEngine;
using Mirror;

// 초롱불 벽 걸기 온라인 연결
// [로컬 클라] PlayerLanternPlacement.PlacementRequested를 받아 조준 Ray만 서버로 보냄
// [서버] 요청자, 라운드, 생존, 장착을 확인하고, 벽 판정 + 소지품 차감 + 생성은 PlayerLanternPlacemenr가 한 번에 처리
// [모든 클라] NetworkServer.Spawn으로 위치·회전이 전달됨 (NetLanternWorldItem은 Rigidbody가 없어 mounted 동기화 x)

[RequireComponent(typeof(NetworkIdentity), typeof(NetPlayerEquipment), typeof(NetPlayerInventory))]
public class NetPlayerLanternPlacement : NetworkBehaviour
{
    private const string LanternItemId = "item_lantern";

    private PlayerLanternPlacement _placement;
    private NetPlayerEquipment _equipment;
    private NetPlayerStatus _status;
    private long _lastRequestId;        // [서버] 이 플레이어가 보낸 마지막 요청 번호

    private void Awake()
    {
        _equipment = GetComponent<NetPlayerEquipment>();
        _status = GetComponent<NetPlayerStatus>();
    }

    // PlayerInteractor.Awake가 자동으로 붙이지만, Awake 순서는 보장되지 않기에 쓸 때 찾음
    private PlayerLanternPlacement Placement
    {
        get
        {
            if (_placement == null) _placement = GetComponent<PlayerLanternPlacement>();
            if (_placement == null) _placement = gameObject.AddComponent<PlayerLanternPlacement>();

            return _placement;
        }
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Placement.PlacementRequested += HandlePlacementRequested;
    }

    public override void OnStopLocalPlayer()
    {
        if (_placement != null) _placement.PlacementRequested -= HandlePlacementRequested;
        base.OnStopLocalPlayer();
    }

    // [로컬 클라] 벽 위치가 아니라 조준 Ray만 보냄. 벽, 거리, 여유 공간은 서버가 다시 판정
    private void HandlePlacementRequested(long requestId, Ray aim)
    {
        CmdRequestPlace(requestId, aim);
    }

    // [서버] Command는 자기 플레이어 오브젝트에서만 받으므로 요청자 소유권은 Mirror가 보장
    [Command]
    private void CmdRequestPlace(long requestId, Ray aim)
    {
        string reject = ServerValidate(requestId);
        if (reject != null)
        {
            TargetPlaceFailed(reject);
            return;
        }

        _lastRequestId = requestId;
        bool success = Placement.TryPlaceAuthoritative(requestId, aim,
            () => ServerValidateState() == null,
            ServerResolvePrefab, ServerPublish, ServerRollback,
            out WorldItem placed, authoritativeEquippedItem: _equipment.EquippedItem);

        if (!success)
        {
            TargetPlaceFailed("여기에는 초롱불을 걸 수 없습니다.");
            return;
        }

        RpcPresentPlaced(requestId, placed.transform.position);
    }

    [Server]
    private string ServerValidate(long requestId)
    {
        if (requestId <= _lastRequestId) return "이미 처리된 요청입니다.";
        return ServerValidateState();
    }

    [Server]
    private string ServerValidateState()
    {
        if (_status != null && _status.IsDead) return "지금은 걸 수 없습니다.";

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return "지금은 걸 수 없습니다.";

        // 클라가 보낸 아이템이 아니라 서버가 확정한 장착 아이템 기준
        ItemData equipped = _equipment.EquippedItem;
        if (equipped == null || equipped.ItemId != LanternItemId) return "초롱불을 들고 있어야 합니다.";

        return null;
    }

    // [서버] spawnPrefabs에서 같은 ItemData를 가진 네트워크 프리팹 (NetLanternWorldItem)
    [Server]
    private static GameObject ServerResolvePrefab(ItemData item)
    {
        foreach (GameObject prefab in NetworkManager.singleton.spawnPrefabs)
        {
            if (prefab != null && prefab.TryGetComponent(out WorldItem world) && world.ItemData == item) return prefab;
        }

        return null;
    }

    // [서버] 위치가 정해져 활성화된 초롱불을 네트워크에 공개
    [Server]
    private bool ServerPublish(WorldItem world)
    {
        if (world == null || !world.TryGetComponent(out NetworkIdentity identity)) return false;

        NetworkServer.Spawn(world.gameObject);
        return identity.netId != 0;
    }

    // [서버] 공개 직후 실패하면 클라에서 지움, 서버 오브젝트 파괴는 PlayerLanternPlacement가 함
    [Server]
    private void ServerRollback(WorldItem world)
    {
        if (world != null && world.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0)
            NetworkServer.UnSpawn(world.gameObject);
    }

    [TargetRpc]
    private void TargetPlaceFailed(string message)
    {
        if (TryGetComponent(out PlayerItemFeedback feedback)) feedback.Unavailable(message);
    }

    [ClientRpc]
    private void RpcPresentPlaced(long requestId, Vector3 position)
    {
        Placement.PresentPlacementConfirmed(requestId, position);
    }
}
