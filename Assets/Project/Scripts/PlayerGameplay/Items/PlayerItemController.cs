using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _itemHoldPoint;

    public int SelectedSlotIndex { get; private set; } = -1;

    public ItemData EquippedItemData { get; private set; }

    public bool HasEquippedItem =>
        _equippedItem != null;

    public bool CanUseItems { get; set; } = true;

    private PlayerInputReader _inputReader;
    private PlayerInventory _inventory;

    private PlayerInspectController _inspectController;
    private PlayerObservationController _observationController;

    private GameObject _equippedObject;
    private ItemBase _equippedItem;

    private void Awake()
    {
        _inputReader =
            GetComponent<PlayerInputReader>();

        _inventory =
            GetComponent<PlayerInventory>();

        _inspectController =
            GetComponent<PlayerInspectController>();

        _observationController =
            GetComponent<PlayerObservationController>();
    }

    private void OnEnable()
    {
        _inputReader.SlotSelected += SelectSlot;

        _inputReader.UseItemStarted +=
            UseItemStarted;

        _inputReader.UseItemCanceled +=
            UseItemCanceled;
    }

    private void OnDisable()
    {
        if (_inputReader == null)
        {
            return;
        }

        _inputReader.SlotSelected -= SelectSlot;

        _inputReader.UseItemStarted -=
            UseItemStarted;

        _inputReader.UseItemCanceled -=
            UseItemCanceled;
    }

    private void SelectSlot(int slotIndex)
    {
        if (!CanUseItems)
        {
            return;
        }

        if (slotIndex < 0 ||
            slotIndex >= _inventory.QuickSlotCount)
        {
            return;
        }

        SelectedSlotIndex = slotIndex;

        ItemData itemData =
            _inventory.GetQuickSlotItem(slotIndex);

        if (itemData == null)
        {
            UnequipCurrentItem();

            Debug.Log(
                $"[Item] 슬롯 {slotIndex + 1}: " +
                "비어 있음"
            );

            return;
        }

        EquipItem(itemData);

        Debug.Log(
            $"[Item] 슬롯 {slotIndex + 1} 선택: " +
            $"{itemData.DisplayName}"
        );
    }

    private void EquipItem(ItemData itemData)
    {
        if (!CanUseItems)
        {
            return;
        }

        if (itemData == null)
        {
            return;
        }

        if (ReferenceEquals(
                EquippedItemData,
                itemData) &&
            _equippedItem != null)
        {
            return;
        }

        UnequipCurrentItem();

        if (_itemHoldPoint == null)
        {
            Debug.LogWarning(
                "[Item] ItemHoldPoint가 " +
                "지정되지 않았습니다.",
                gameObject
            );

            return;
        }

        if (itemData.HeldPrefab == null)
        {
            Debug.LogWarning(
                $"[Item] {itemData.DisplayName}의 " +
                "Held Prefab이 없습니다.",
                itemData
            );

            return;
        }

        _equippedObject =
            Instantiate(
                itemData.HeldPrefab,
                _itemHoldPoint
            );

        _equippedObject.transform.localPosition =
            Vector3.zero;

        _equippedObject.transform.localRotation =
            Quaternion.identity;

        _equippedItem =
            _equippedObject.GetComponent<ItemBase>();

        if (_equippedItem == null)
        {
            Debug.LogWarning(
                $"[Item] {itemData.DisplayName}의 " +
                "Held Prefab에 ItemBase 계열 " +
                "컴포넌트가 없습니다.",
                _equippedObject
            );

            Destroy(_equippedObject);

            _equippedObject = null;

            return;
        }

        EquippedItemData =
            itemData;

        _equippedItem.Initialize(
            itemData,
            gameObject
        );

        _equippedItem.OnEquipped();

        Debug.Log(
            $"[Item] 장착: " +
            $"{itemData.DisplayName}",
            _equippedObject
        );
    }

    public void UnequipCurrentItem()
    {
        if (_equippedItem != null)
        {
            _equippedItem.OnUnequipped();
        }

        if (_equippedObject != null)
        {
            Destroy(_equippedObject);
        }

        _equippedObject = null;
        _equippedItem = null;
        EquippedItemData = null;
    }

    private void UseItemStarted()
    {
        if (!CanUseItem())
        {
            return;
        }

        _equippedItem.OnUseStarted();
    }

    private void UseItemCanceled()
    {
        if (!CanUseItems)
        {
            return;
        }

        if (_equippedItem == null)
        {
            return;
        }

        _equippedItem.OnUseCanceled();
    }

    private bool CanUseItem()
    {
        if (!CanUseItems)
        {
            return false;
        }

        if (_equippedItem == null)
        {
            return false;
        }

        if (_inspectController != null &&
            _inspectController.IsInspecting)
        {
            return false;
        }

        if (_observationController != null &&
            _observationController.IsObserving)
        {
            return false;
        }

        return true;
    }
}