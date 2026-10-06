using UnityEngine;
using Exorcist.FirstPerson;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _itemHoldPoint;

    [Header("First Person Hand")]
    [SerializeField] private FirstPersonRightHand _rightHand;

    public int SelectedSlotIndex { get; private set; } = -1;

    public ItemData EquippedItemData { get; private set; }

    // ItemBase가 없는 Visual Only 아이템도 장착 상태로 취급
    public bool HasEquippedItem =>
        EquippedItemData != null;

    public bool CanUseItems { get; set; } = true;

    private PlayerInputReader _inputReader;
    private PlayerInventory _inventory;
    private PlayerViewModeController _viewModeController;

    private GameObject _equippedObject;
    private ItemBase _equippedItem;

    private void Awake()
    {
        _inputReader =
            GetComponent<PlayerInputReader>();

        _inventory =
            GetComponent<PlayerInventory>();

        _viewModeController =
            GetComponent<PlayerViewModeController>();

        // 게임 시작 시에는 오른손을 숨긴다.
        if (_rightHand != null)
        {
            _rightHand.gameObject.SetActive(false);
        }
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
        if (!CanUseItems ||
            itemData == null)
        {
            return;
        }

        if (ReferenceEquals(
                EquippedItemData,
                itemData))
        {
            return;
        }

        UnequipCurrentItem();

        // =====================================================
        // 오른손 시스템을 사용하는 아이템
        // =====================================================

        if (_rightHand != null &&
            itemData.HandProfile != null)
        {
            // 아이템을 장착할 때만 오른손 표시
            _rightHand.gameObject.SetActive(true);

            _rightHand.Equip(
                itemData.HandProfile
            );

            _equippedObject =
                _rightHand.EquippedInstance;

            if (_equippedObject != null)
            {
                _equippedItem =
                    _equippedObject
                        .GetComponentInChildren<ItemBase>(true);
            }

            EquippedItemData = itemData;

            InitializeEquippedItem(itemData);

            Debug.Log(
                $"[Item] 오른손 장착: " +
                $"{itemData.DisplayName}",
                gameObject
            );

            return;
        }

        // =====================================================
        // HandProfile이 없는 기존 아이템 Fallback
        // =====================================================

        if (_itemHoldPoint == null)
        {
            Debug.LogWarning(
                "[Item] ItemHoldPoint가 지정되지 않았습니다.",
                gameObject
            );

            return;
        }

        if (itemData.VisualPrefab == null)
        {
            Debug.LogWarning(
                $"[Item] {itemData.DisplayName}의 " +
                "Visual Prefab이 없습니다.",
                itemData
            );

            return;
        }

        _equippedObject =
            Instantiate(
                itemData.VisualPrefab,
                _itemHoldPoint
            );

        _equippedObject.transform.localPosition =
            Vector3.zero;

        _equippedObject.transform.localRotation =
            Quaternion.identity;

        _equippedItem =
            _equippedObject
                .GetComponentInChildren<ItemBase>(true);

        EquippedItemData = itemData;

        InitializeEquippedItem(itemData);

        Debug.Log(
            $"[Item] 장착: {itemData.DisplayName}",
            _equippedObject
        );
    }

    private void InitializeEquippedItem(
        ItemData itemData)
    {
        // 단순 Visual 아이템은 ItemBase가 없어도 정상 장착
        if (_equippedItem == null)
        {
            Debug.Log(
                $"[Item] {itemData.DisplayName}은 " +
                "Visual Only 아이템으로 장착됩니다.",
                gameObject
            );

            return;
        }

        _equippedItem.Initialize(
            itemData,
            gameObject
        );

        _equippedItem.OnEquipped();
    }

    public void UnequipCurrentItem()
    {
        if (_equippedItem != null)
        {
            _equippedItem.OnUnequipped();
        }

        // 오른손 시스템으로 생성한 아이템이면
        // FirstPersonRightHand가 직접 제거
        if (_rightHand != null &&
            _rightHand.CurrentItem != null)
        {
            _rightHand.Unequip();

            // 아이템이 없을 때는 손도 숨김
            _rightHand.gameObject.SetActive(false);
        }
        else if (_equippedObject != null)
        {
            // 기존 ItemHoldPoint 방식의 아이템
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
        if (!CanUseItems ||
            _equippedItem == null)
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

        // Visual Only 아이템은 손에 들 수는 있지만
        // 실제 사용 동작은 실행하지 않는다.
        if (_equippedItem == null)
        {
            return false;
        }

        if (_viewModeController != null &&
            _viewModeController.IsBusy)
        {
            return false;
        }

        return true;
    }
}