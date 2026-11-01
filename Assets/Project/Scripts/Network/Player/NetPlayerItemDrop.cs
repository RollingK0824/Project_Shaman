using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkIdentity), typeof(PlayerItemDropController), typeof(NetPlayerInventory))]
public class NetPlayerItemDrop : NetworkBehaviour
{
    [SerializeField] private NetItemDataBase _dataBase;

    private PlayerItemDropController _drops;
    private NetPlayerStatus _status;
    private long _lastRequestId;    // [서버] 이 플레이어가 보낸 마지막 요청 번호

    private void Awake()
    {
        _drops = GetComponent<PlayerItemDropController>();
        _status = GetComponent<NetPlayerStatus>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _drops.SetAuthorityCheck(() => isServer);
        _drops.SetWorldPublisher(ServerPublish, ServerRollback);
        _drops.SetWorldPrefabResolver(ServerResolvePrefab);
        _drops.SetCarriedWorldObjectCheck(item => ProjectShaman.Network.AI.Net_ToolItem.IsStowedBy(gameObject, item));
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        _drops.DropRequested += HandleDropRequested;
        _drops.DrawingDropRequested += HandleDrawingDropRequested;
    }

    public override void OnStopLocalPlayer()
    {
        _drops.DropRequested -= HandleDropRequested;
        _drops.DrawingDropRequested -= HandleDrawingDropRequested;
        base.OnStopLocalPlayer();
    }

    // [로컬 클라] IteamData 대신 DB 번호로 보냄
    private void HandleDropRequested(long requestId, ItemData item)
    {
        int itemId = _dataBase.GetId(item);
        if (itemId < 0)
        {
            _drops.ApplyDropResult(requestId, false, "버릴 수 없는 아이템입니다.");
            return;
        }

        CmdRequestDrop(requestId, itemId);
    }

    private void HandleDrawingDropRequested(long requestId, string id) => CmdRequestDrawingDrop(requestId, id);

    [Command]
    private void CmdRequestDrawingDrop(long requestId, string id)
    {
        var collection = GetComponent<PlayerTalismanCollection>();
        GameManager gm = GameManager.Instance;
        bool valid = requestId > _lastRequestId && !string.IsNullOrEmpty(id) && id.Length <= 64 &&
            (_status == null || !_status.IsDead) && gm != null && gm.CurrentGameState == GameState.Ongoing &&
            collection != null && collection.Find(id, out _) != null;
        if (!valid) { TargetDropResult(requestId, false, "부적을 버릴 수 없습니다."); return; }
        _lastRequestId = requestId;
        bool success = _drops.TryDropDrawingFinal(requestId, id, out _);
        TargetDropResult(requestId, success, _drops.LastMessage);
    }

    // [서버] Command는 자기 플레이어 오브젝트에서만 받으므로 요청자 소유권은 Mirror가 보장
    [Command]
    private void CmdRequestDrop(long requestId, int itemId)
    {
        ItemData item = _dataBase.GetItem(itemId);
        string reject = ServerValidate(requestId, item);
        if (reject != null)
        {
            TargetDropResult(requestId, false, reject);
            return;
        }

        _lastRequestId = requestId;
        bool success = _drops.TryDropFinal(requestId, item, out _);
        TargetDropResult(requestId, success, _drops.LastMessage);
    }

    [Server]
    private string ServerValidate(long requestId, ItemData item)
    {
        if (requestId <= _lastRequestId) return "이미 처리된 요청입니다.";
        if (_status != null && _status.IsDead) return "지금은 버릴 수 없습니다.";

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return "지금은 버릴 수 없습니다.";

        if (item == null) return "알 수 없는 아이템입니다.";

        // 보유 수량, 공용 재료, 수첩, 기본 지급, 월드 프리팹 유무
        // 효과 부적은 그리면 인벤토리에서 빈 부적 1장이 빠지고 별도 목록으로 옮겨지므로,
        // 여기서 버려지는 효과 부적은 항상 빈 부적. 그린 부적은 버리기 경로로 들어오지 않음
        return _drops.CanDrop(item, out string reason) ? null : reason;
    }

    // [서버] 드롭 컨트롤러가 놓을 자리를 정해 생성한 아이템을 네트워크에 공개
    [Server]
    private bool ServerPublish(WorldItem world)
    {
        if (world == null || !world.TryGetComponent(out NetworkIdentity identity)) return false;

        // 버린 아이템만 물리로 떨어뜨림 (Spawn 전에 들어 올려야 클라도 같은 위치에서 시작)
        if (world.TryGetComponent(out NetWorldItemPhysics physics)) physics.ServerBeginDrop();

        NetworkServer.Spawn(world.gameObject);
        return identity.netId != 0;
    }

    // [서버] 공개 직후 실패하면 클라에서 지움, 서버 오브젝트 파괴는 드롭 컨트롤러가 함
    [Server]
    private void ServerRollback(WorldItem world)
    {
        if (world != null && world.TryGetComponent(out NetworkIdentity identity) && identity.netId != 0)
            NetworkServer.UnSpawn(world.gameObject);
    }

    [Server]
    private static GameObject ServerResolvePrefab(ItemData item)
    {
        foreach (GameObject prefab in NetworkManager.singleton.spawnPrefabs)
        {
            if (prefab != null && prefab.TryGetComponent(out WorldItem world) && world.ItemData == item) return prefab;
        }

        return null;
    } 

    [TargetRpc]
    private void TargetDropResult(long requestId, bool success, string message)
    {
        _drops.ApplyDropResult(requestId, success, message);
    }
}
