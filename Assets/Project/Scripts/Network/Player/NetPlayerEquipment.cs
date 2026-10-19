using UnityEngine;
using Mirror;
using System;
using ProjectShaman.ItemHold;


// [로컬 클라]      장착 아이템 변화를 감지해서 서버로 전달
// [서버]          보유 여부를 확인하고 장착 아이템 ID를 확정 모든 클라 공개 (Sync Mode = Observers)
// [모든 클라]      다른 플레이어에게 보이는 3인칭 장착 표시는 EquippedItemChanged에 연결
// [다른 클라, 호스트] 원격 사본은 모델은 PlayerItemController.ApplyRemoteHeldVisual, 팔 자세는 여기서 ItemHoldDriver로 직접 구동
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerItemController))]
public class NetPlayerEquipment : NetworkBehaviour
{
    [SerializeField] private NetItemDataBase _dataBase;

    // [서버 -> 모든 클라] 장착 아이템 ID, -1이면 장착 x
    [SyncVar(hook = nameof(OnEquippedItemIdChanged))] private int _equippedItemId = -1;

    private PlayerItemController _itemController;
    private PlayerInventory _inventory;
    private NetPlayerStatus _status;
    private ItemHoldDriver _holdDriver;

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
        _holdDriver = GetComponentInChildren<ItemHoldDriver>(true);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        GetComponent<PlayerHealth>().Died += HandleDied;
    }
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (isLocalPlayer) return;

        // PlayerItemController가 OnEnable에서 먼저 구독하므로 모델 제거 → 이 핸들러 순서로 호출됨
        if (_holdDriver != null) _holdDriver.Unequipped += HandleRemoteHoldUnequipped;
        ApplyRemoteHeld(EquippedItem);
    }
    public override void OnStopClient()
    {
        if (!isLocalPlayer && _holdDriver != null) _holdDriver.Unequipped -= HandleRemoteHoldUnequipped;
        base.OnStopClient();
    }
    public override void OnStopServer()
    {
        GetComponent<PlayerHealth>().Died -= HandleDied;
        base.OnStopServer();
    }
    private void HandleDied(GameObject source) => _equippedItemId = -1;

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

        // 로컬 플레이어는 PlayerItemController가 이미 1~3인칭표시중
        if (!isLocalPlayer) ApplyRemoteHeld(item);

        EquippedItemChanged?.Invoke(item);
    }

    // [다른 클라, 호스트] 확정된 장착 아이템으로 원격 사본의 3인칭 모델과 팔 자세를 맞춤
    // 상체 레이어는 전이 없이 ItemHoldDriver가 직접 재생하는 구조라 NetworkAnimator로는 블렌드가 따라오지 않음
    private void ApplyRemoteHeld(ItemData item)
    {
        if (_holdDriver == null)
        {
            _itemController.ApplyRemoteHeldVisual(item);
            return;
        }

        bool canAct = PlayerActionGuard.CanAct(gameObject);
        bool holding = canAct && item != null && item.HoldType != HoldType.Empty;

        if (!holding)
        {
            // 손을 내리는 중에는 모델을 남겨 두고, 다 내려가면 PlayerItemController가 제거
            if (_holdDriver.Phase != HoldPhase.Empty) _holdDriver.Unequip();
            if (!canAct || _holdDriver.Phase == HoldPhase.Empty) _itemController.ApplyRemoteHeldVisual(null);
            return;
        }

        _itemController.ApplyRemoteHeldVisual(item);
        _holdDriver.SetHoldType((int)item.HoldType);
        _holdDriver.Equip();   // 내리는 중이면 다 내린 뒤 다시 들어 올림
    }

    // [다른 클라, 호스트] 손이 다 내려간 시점에 PlayerItemController가 모델을 지움
    // 그 사이 새 아이템이 확정됐으면 모델을 다시 붙임 (같은 아이템이면 ApplyRemoteHeldVisual이 무시)
    private void HandleRemoteHoldUnequipped()
    {
        _itemController.ApplyRemoteHeldVisual(PlayerActionGuard.CanAct(gameObject) ? EquippedItem : null);
    }

    // [다른 클라, 호스트] 서버가 확정한 사용의 3인칭 팔 모션만 재생. 판정·소모·소리 없음
    // NetPlayerItemUse.RpcPlayUsePresentation에서 호출
    public void PlayRemoteUseMotion(ItemData item)
    {
        if (isLocalPlayer || _holdDriver == null || item == null) return;
        if (item != EquippedItem || !_holdDriver.IsEquipped) return;

        _holdDriver.Use(item.ThirdPersonUseMotionId);
    }
}
