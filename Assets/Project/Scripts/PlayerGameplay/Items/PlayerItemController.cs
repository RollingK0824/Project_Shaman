using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _itemHoldPoint;

    public int SelectedSlotIndex { get; private set; } = -1;
    public ItemData EquippedItemData { get; private set; }

    private PlayerInputReader _inputReader;
    private PlayerInventory _inventory;

    private GameObject _equippedObject;
    private ItemBase _equippedItem;

    private void Awake()
    {
        _inputReader =
            GetComponent<PlayerInputReader>();

        _inventory =
            GetComponent<PlayerInventory>();
    }

    private void OnEnable()
    {
        _inputReader.SlotSelected += SelectSlot;
        _inputReader.UseItemStarted += UseItemStarted;
        _inputReader.UseItemCanceled += UseItemCanceled;
    }

    private void OnDisable()
    {
        if (_inputReader == null)
        {
            return;
        }

        _inputReader.SlotSelected -= SelectSlot;
        _inputReader.UseItemStarted -= UseItemStarted;
        _inputReader.UseItemCanceled -= UseItemCanceled;
    }

    private void SelectSlot(int slotIndex)
    {
        if (slotIndex < 0 ||
            slotIndex >= 6)
        {
            return;
        }

        ItemData itemData =
            _inventory.GetQuickSlotItem(slotIndex);

        SelectedSlotIndex = slotIndex;

        if (itemData == null)
        {
            UnequipCurrentItem();

            Debug.Log(
                $"[Item] 슬롯 {slotIndex + 1}: 비어 있음"
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
        UnequipCurrentItem();

        if (itemData == null ||
            itemData.HeldPrefab == null ||
            _itemHoldPoint == null)
        {
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

        _equippedObject.transform.localScale =
            Vector3.one;

        _equippedItem =
            _equippedObject.GetComponent<ItemBase>();

        if (_equippedItem == null)
        {
            Debug.LogWarning(
                $"[Item] {itemData.DisplayName}의 " +
                "Held Prefab에 ItemBase가 없습니다.",
                _equippedObject
            );

            Destroy(_equippedObject);

            _equippedObject = null;
            EquippedItemData = null;

            return;
        }

        EquippedItemData = itemData;

        _equippedItem.Initialize(
            itemData,
            gameObject
        );
    }

    private void UnequipCurrentItem()
    {
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
        if (_equippedItem == null)
        {
            return;
        }

        _equippedItem.OnUseStarted();
    }

    private void UseItemCanceled()
    {
        if (_equippedItem == null)
        {
            return;
        }

        _equippedItem.OnUseCanceled();
    }
}