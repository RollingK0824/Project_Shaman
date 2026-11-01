using UnityEngine;
using Mirror;

// [클라] 줍기 요청을 서버로 전달 / [서버] 줍기를 판정하고, 보유 목록 원본을 보관
// 보유 목록은 소유 클라이언트에서만 동기화 (Sync Mode = Owner)
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerInventory))]
public class NetPlayerInventory : NetworkBehaviour
{
    [SerializeField] private NetItemDataBase _dataBase;
    [SerializeField] private float _maxPickupDistance = 4.5f;

    // [서버 -> 소유 클라] 보유 아이템 ID 목록
    private readonly SyncList<int> _itemIds = new SyncList<int>();

    private PlayerInventory _inventory;
    private NetPlayerStatus _status;

    private void Awake()
    {
        _inventory = GetComponent<PlayerInventory>();
        _status = GetComponent<NetPlayerStatus>();
    }

    private void Reset()
    {
        syncMode = SyncMode.Owner;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _inventory.ItemUnitsChanged += ServerMirrorUnits;
        if (NetSharedMaterials.Instance != null) _inventory.BindSharedMaterials(NetSharedMaterials.Instance.Store);
    }

    public override void OnStopServer()
    {
        _inventory.ItemUnitsChanged -= ServerMirrorUnits;

        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        _itemIds.OnAdd += HandleItemIdAdded;
        _itemIds.OnRemove += HandleItemIdRemoved;
        if (NetSharedMaterials.Instance != null) _inventory.BindSharedMaterials(NetSharedMaterials.Instance.Store);
    }

    public override void OnStopClient()
    {
        _itemIds.OnAdd -= HandleItemIdAdded;
        _itemIds.OnRemove -= HandleItemIdRemoved;
        
        base.OnStopClient();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        PlayerEvents.PickupRequested += HandlePickupRequested;
    }

    public override void OnStopLocalPlayer()
    {
        PlayerEvents.PickupRequested -= HandlePickupRequested;
        base.OnStopLocalPlayer();
    }

    // [로컬 클라] 이벤트 버스로 들어온 줍기 요청을 서버(Cmd)로 전달
    private void HandlePickupRequested(GameObject interactor, WorldItem item)
    {
        if (interactor != gameObject) return;
        if (!item.TryGetComponent(out NetworkIdentity itemIdentity)) return;

        CmdRequestPickup(itemIdentity);
    }


    // [서버] 줍기 판정 이미 다른 사람이 주워 사라진 아이템이라면 itemIdentity가 null
    [Command]
    private void CmdRequestPickup(NetworkIdentity itemIdentity)
    {
        if (itemIdentity == null || !PlayerActionGuard.CanAct(gameObject)) return;
        if (_status != null && _status.IsDead) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return;

        if (!itemIdentity.TryGetComponent(out WorldItem item)) return;
        if (!item.CanInteract(gameObject)) return;
        if (Vector3.Distance(transform.position, item.transform.position) > _maxPickupDistance) return;


        ItemData itemData = item.ItemData;

        if (_dataBase.GetId(itemData) < 0)
        {
            Debug.LogError($"[NetPlayerInventory] NetItemDataBase에 없는 아이템: {itemData.DisplayName}", this);
            return;
        }

        // 인벤토리에 먼저 넣어보고 성공했을때만 월드 아이템 제거
        if (!(item.HasDrawing ? item.TryCollectDrawing(gameObject) : _inventory.TryAddItem(itemData)))
        {
            Debug.LogWarning($"[NetPlayerInventory] 획득 거절 : {name} <- {itemData.DisplayName}", this);
            return;
        }

        if (!NetItemSpawner.ServerDespawnFromAny(item.gameObject))
        {
            NetworkServer.Destroy(item.gameObject);
        }

        //Debug.Log($"[NetPlayerInventory] 획득 판정 : {name} <- {itemData.DisplayName} (id : {id})", this);
    }

    // [서버] 아이템 차감. 서버 인벤토리와 소유 클라에 보내는 보유 목록에 함꼐 줄임
    // 아이템 사용 소모, 크래프팅 재료, 버리기 등 모든 차감이 이 함수를 지나감
    [Server]
    public bool ServerConsume(ItemData itemData, int amount = 1)
    {
        if (itemData == null || amount <= 0) return false;

        int id = _dataBase.GetId(itemData);
        if (id < 0)
        {
            Debug.LogError($"[NetPlayerInventory] NetItemDataBase에 없는 아이템: {itemData.DisplayName}", this);
            return false;
        }

        // 서버 인벤토리부터 확인하고 차감, 실패하면 목록 건드리지 않음
        if (!_inventory.HasItem(itemData, amount)) return false;
        if (!_inventory.ConsumeItem(itemData, amount)) return false;

        Debug.Log($"[NetPlayerInventory] 차감 판정 : {name} -> {itemData.DisplayName} x {amount} (id : {id})", this);

        return true;
    }

    [Server]
    private void ServerMirrorUnits(ItemData itemData, int delta)
    {
        int id = _dataBase.GetId(itemData);
        if (id < 0)
        {
            Debug.LogError($"[NetPlayerInventory] NetItemDataBase에 없는 아이템: {itemData.DisplayName}", this);
            return;
        }

        for (int i = 0; i < delta; i++) _itemIds.Add(id);
        for (int i = 0; i < -delta; i++) _itemIds.Remove(id);
    }

    private void HandleItemIdAdded(int index)
    {
        if (isServer) return;

        ItemData itemData = _dataBase.GetItem(_itemIds[index]);
        if (itemData != null) _inventory.TryAddItem(itemData);
    }

    private void HandleItemIdRemoved(int index, int removedId)
    {
        if (isServer) return;

        ItemData itemData = _dataBase.GetItem(removedId);

        if (itemData == null) return;

        bool removed = itemData.IsPermanent ? _inventory.TryDropUnit(itemData, () => true) : _inventory.RemoveItem(itemData, 1);

        if (!removed)
        {
            Debug.LogWarning($"[NetPlayerInventory] 서버와 클라 수량 어긋남: {itemData.DisplayName}", this);
        }
    }


#if UNITY_EDITOR
    // [테스트 전용]
    [ContextMenu("Test/Consume First Item(Server)")]
    private void TestConsumeFirstItem()
    {
        if (!isServer || _itemIds.Count == 0)
        {
            Debug.LogWarning("[NerPlayerInventory] 호스트에서, 아이템 하나 이상 주운 뒤 실행해주세요", this);
            return;
        }

        ServerConsume(_dataBase.GetItem(_itemIds[0]), 1);
    }
#endif
}
