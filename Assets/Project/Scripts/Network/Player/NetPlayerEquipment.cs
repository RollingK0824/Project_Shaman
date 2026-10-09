using UnityEngine;
using Mirror;
using System;


// [로컬 클라]      장착 아이템 변화를 감지해서 서버로 전달
// [서버]          보유 여부를 확인하고 장착 아이템 ID를 확정 모든 클라 공개 (Sync Mode = Observers)
// [모든 클라]      다른 플레이어에게 보이는 3인칭 장착 표시는 EquippedItemChanged에 연결
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerItemController))]
public class NetPlayerEquipment : NetworkBehaviour
{
    [SerializeField] private NetItemDataBase _dataBase;

    // [서버 -> 모든 클라] 장착 아이템 ID, -1이면 장착 x
    [SyncVar(hook = nameof(OnEquippedItemIdChanged))] private int _equippedItemId = -1;

    private PlayerItemController _itemController;
    private PlayerInventory _inventory;
    private NetPlayerStatus _status;

    // [로컬 클라] 마지막 서버에 보낸 장착 아이템. 바뀔때만 보냄
    private ItemData _lastSentItem;

    public int EquippedItemId => _equippedItemId;
    public ItemData EquippedItem => _dataBase != null ? _dataBase.GetItem(_equippedItemId) : null;


    public event Action<ItemData> EquippedItemChanged;

    private void Awake()
    {
        _itemController = GetComponent<PlayerItemController>();
        _inventory = GetComponent<PlayerInventory>();
        _status = GetComponent<NetPlayerStatus>();
    }

    // [로컬] 장착 변화 감지. 플레이어 코드에 이벤트 생기면 이벤트 구독으로 교체
    private void Update()
    {
        if (!isLocalPlayer) return;

        ItemData current = _itemController.EquippedItemData;
        if (ReferenceEquals(current, _lastSentItem)) return;

        _lastSentItem = current;

        int id = current != null ? _dataBase.GetId(current) : -1;
        if (current != null && id < 0)
        {
            Debug.LogWarning($"[NetPlayerEquipment] NetItemDataBase에 없는 아이템이라 다른 플레이어에게 보이지 않습니다: {current.DisplayName}", this);
        }

        CmdSetEquippedItem(id);
    }

    // [서버] 장착 확정 코드 / 해제는 항상 허용, 장착은 살아있고, 실제로 가진 아이템만
    [Command]
    private void CmdSetEquippedItem(int itemId)
    {
        if (itemId < 0)
        {
            _equippedItemId = -1;
            return;
        }

        if (_status != null && _status.IsDead) return;

        ItemData item = _dataBase.GetItem(itemId);
        if (item == null) return;
        if (!_inventory.Contains(item)) return;

        _equippedItemId = itemId;
    }

    // [모든 클라, 호스트] 확정된 장착 아이템 알림
    private void OnEquippedItemIdChanged(int oldId, int newId)
    {
        ItemData item = EquippedItem;
        string itemName = item != null ? item.DisplayName : "없음";
        Debug.Log($"[NetPlayerEquipment] {name} 장착: {itemName}", this);

        EquippedItemChanged?.Invoke(item);
    }
}
