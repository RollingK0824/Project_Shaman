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

    public override void OnStartClient()
    {
        base.OnStartClient();

        _itemIds.OnAdd += HandleItemIdAdded;
    }

    public override void OnStopClient()
    {
        _itemIds.OnAdd -= HandleItemIdAdded;
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

        int id = _dataBase.GetId(itemData);

        if (id < 0)
        {
            Debug.LogError($"[NetPlayerInventory] NetItemDataBase에 없는 아이템: {itemData.DisplayName}", this);
            return;
        }

        if (!NetItemSpawner.ServerDespawnFromAny(item.gameObject))
        {
            NetworkServer.Destroy(item.gameObject);
        }

        _inventory.TryAddItem(itemData);
        _itemIds.Add(id);

        Debug.Log($"[NetPlayerInventory] 획득 판정 : {name} <- {itemData.DisplayName} (id : {id})", this);
    }

    private void HandleItemIdAdded(int index)
    {
        if (isServer) return;

        ItemData itemData = _dataBase.GetItem(_itemIds[index]);
        if (itemData != null) _inventory.TryAddItem(itemData);
    }
}
