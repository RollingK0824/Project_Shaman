using UnityEngine;
using Exorcist.FirstPerson;
using ProjectShaman.ItemHold;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _itemHoldPoint;

    [Header("First Person Hand")]
    [SerializeField] private FirstPersonRightHand _rightHand;

    [Header("Third Person Hold")]
    [SerializeField] private ItemHoldDriver _itemHoldDriver;

    public int SelectedSlotIndex { get; private set; } = -1;

    public ItemData EquippedItemData { get; private set; }

    public bool HasEquippedItem =>
        EquippedItemData != null;

    public bool CanUseItems { get; set; } = true;

    private PlayerInputReader _inputReader;
    private PlayerInventory _inventory;
    private PlayerViewModeController _viewModeController;

    // 1인칭 장착 오브젝트
    private GameObject _equippedObject;
    private ItemBase _equippedItem;

    // 3인칭 장착 오브젝트
    private GameObject _thirdPersonVisual;
    private Renderer[] _firstPersonRenderers;
    private Renderer[] _thirdPersonRenderers;

    // 아이템 교체 시
    // 기존 아이템 Unequip이 끝난 뒤 장착할 아이템
    private ItemData _pendingThirdPersonItem;


    private void Awake()
    {
        _inputReader =
            GetComponent<PlayerInputReader>();

        _inventory =
            GetComponent<PlayerInventory>();

        _viewModeController =
            GetComponent<PlayerViewModeController>();

        // Inspector에서 지정하지 않아도
        // 캐릭터 자식에서 자동으로 찾는다.
        if (_itemHoldDriver == null)
        {
            _itemHoldDriver =
                GetComponentInChildren<ItemHoldDriver>(true);
        }

        // 게임 시작 시에는 1인칭 오른손 숨김
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

        if (_itemHoldDriver != null)
        {
            _itemHoldDriver.Unequipped +=
                OnThirdPersonUnequipped;
        }
    }


    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.SlotSelected -= SelectSlot;

            _inputReader.UseItemStarted -=
                UseItemStarted;

            _inputReader.UseItemCanceled -=
                UseItemCanceled;
        }

        if (_itemHoldDriver != null)
        {
            _itemHoldDriver.Unequipped -=
                OnThirdPersonUnequipped;
        }
    }

    private void LateUpdate()
    {
        // Keep the hand rig running while hiding the camera-only view in third person.
        bool firstPerson = _viewModeController == null ||
            _viewModeController.CurrentViewMode == PlayerViewModeController.ViewMode.FirstPerson;
        SetVisualVisibility(_firstPersonRenderers, firstPerson);
        SetVisualVisibility(_thirdPersonRenderers, !firstPerson);
    }

    private static void SetVisualVisibility(Renderer[] renderers, bool visible)
    {
        if (renderers == null) return;
        foreach (Renderer visual in renderers)
            if (visual != null) visual.enabled = visible;
    }


    // =========================================================
    // Slot
    // =========================================================

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


    // =========================================================
    // Equip
    // =========================================================

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

        // 기존 1인칭 아이템만 먼저 해제한다.
        UnequipFirstPerson();

        // 새로운 아이템을 현재 장착 데이터로 지정
        EquippedItemData = itemData;

        // 3인칭은 자연스럽게:
        // 기존 아이템 내리기 → 교체 → 새 아이템 올리기
        RequestThirdPersonEquip(itemData);

        // 1인칭 장착
        EquipFirstPerson(itemData);

        Debug.Log(
            $"[Item] 장착: {itemData.DisplayName}",
            gameObject
        );
    }


    // =========================================================
    // First Person
    // =========================================================

    private void EquipFirstPerson(ItemData itemData)
    {
        // -----------------------------------------------------
        // FirstPersonRightHand 사용
        // -----------------------------------------------------

        if (_rightHand != null &&
            itemData.HandProfile != null)
        {
            _rightHand.gameObject.SetActive(true);

            _rightHand.Equip(
                itemData.HandProfile
            );

            _equippedObject =
                _rightHand.EquippedInstance;
            _firstPersonRenderers = _rightHand.GetComponentsInChildren<Renderer>(true);

            if (_equippedObject != null)
            {
                _equippedItem =
                    _equippedObject
                        .GetComponentInChildren<ItemBase>(true);
            }

            InitializeEquippedItem(itemData);

            Debug.Log(
                $"[Item] 1인칭 오른손 장착: " +
                $"{itemData.DisplayName}",
                gameObject
            );

            return;
        }


        // -----------------------------------------------------
        // 기존 ItemHoldPoint Fallback
        // -----------------------------------------------------

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
        _firstPersonRenderers = _equippedObject.GetComponentsInChildren<Renderer>(true);

        _equippedItem =
            _equippedObject
                .GetComponentInChildren<ItemBase>(true);

        InitializeEquippedItem(itemData);
    }


    private void InitializeEquippedItem(
        ItemData itemData)
    {
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


    private void UnequipFirstPerson()
    {
        if (_equippedItem != null)
        {
            _equippedItem.OnUnequipped();
        }

        if (_rightHand != null &&
            _rightHand.CurrentItem != null)
        {
            _rightHand.Unequip();
            _rightHand.gameObject.SetActive(false);
        }
        else if (_equippedObject != null)
        {
            Destroy(_equippedObject);
        }

        _equippedObject = null;
        _equippedItem = null;
        _firstPersonRenderers = null;
    }


    // =========================================================
    // Third Person
    // =========================================================

    private void RequestThirdPersonEquip(
        ItemData itemData)
    {
        if (_itemHoldDriver == null)
        {
            return;
        }

        _pendingThirdPersonItem = itemData;

        // 현재 아이템을 들고 있는 중이면
        // 우선 자연스럽게 내린다.
        if (_itemHoldDriver.Phase != HoldPhase.Empty)
        {
            _itemHoldDriver.Unequip();
            return;
        }

        // 이미 빈손이라면 바로 새 아이템 장착
        ApplyPendingThirdPersonItem();
    }


    private void OnThirdPersonUnequipped()
    {
        // 손이 완전히 내려간 시점에
        // 기존 3인칭 아이템을 제거
        DestroyThirdPersonVisual();

        // 교체할 아이템이 있다면 새로 장착
        ApplyPendingThirdPersonItem();
    }


    private void ApplyPendingThirdPersonItem()
    {
        if (_itemHoldDriver == null ||
            _pendingThirdPersonItem == null)
        {
            return;
        }

        ItemData itemData =
            _pendingThirdPersonItem;

        _pendingThirdPersonItem = null;

        DestroyThirdPersonVisual();

        if (itemData.HoldType == HoldType.Empty)
        {
            return;
        }

        SpawnThirdPersonVisual(itemData);

        _itemHoldDriver.SetHoldType(
            (int)itemData.HoldType
        );

        _itemHoldDriver.Equip();

        Debug.Log(
            $"[Item] 3인칭 장착: " +
            $"{itemData.DisplayName} / " +
            $"{itemData.HoldType}",
            gameObject
        );
    }


    private void SpawnThirdPersonVisual(
        ItemData itemData)
    {
        if (_itemHoldDriver == null ||
            _itemHoldDriver.ItemSocket == null)
        {
            Debug.LogWarning(
                "[Item] RightHand_ItemSocket을 찾을 수 없습니다.",
                gameObject
            );

            return;
        }

        GameObject prefab =
            itemData.ThirdPersonVisualPrefab;

        if (prefab == null)
        {
            Debug.LogWarning(
                $"[Item] {itemData.DisplayName}의 " +
                "3인칭 Visual Prefab이 없습니다.",
                itemData
            );

            return;
        }

        _thirdPersonVisual =
            Instantiate(
                prefab,
                _itemHoldDriver.ItemSocket,
                false
            );

        Transform visualTransform =
            _thirdPersonVisual.transform;

        visualTransform.localPosition =
            itemData.ThirdPersonLocalPosition;

        visualTransform.localRotation =
            Quaternion.Euler(
                itemData.ThirdPersonLocalEuler
            );

        visualTransform.localScale =
            itemData.ThirdPersonLocalScale;
        _thirdPersonRenderers = _thirdPersonVisual.GetComponentsInChildren<Renderer>(true);

        // 손에 든 아이템은 월드 충돌 대상이 아니므로
        // Collider를 끈다.
        Collider[] colliders =
            _thirdPersonVisual
                .GetComponentsInChildren<Collider>(true);

        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        Rigidbody[] rigidbodies =
            _thirdPersonVisual
                .GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }


    private void DestroyThirdPersonVisual()
    {
        if (_thirdPersonVisual == null)
        {
            return;
        }

        Destroy(_thirdPersonVisual);

        _thirdPersonVisual = null;
        _thirdPersonRenderers = null;
    }


    // =========================================================
    // Unequip
    // =========================================================

    public void UnequipCurrentItem()
    {
        // 새로운 아이템을 기다리고 있던 상태도 취소
        _pendingThirdPersonItem = null;

        UnequipFirstPerson();

        if (_itemHoldDriver != null)
        {
            if (_itemHoldDriver.Phase != HoldPhase.Empty)
            {
                // 아이템을 든 채 자연스럽게 팔을 내린 뒤
                // OnThirdPersonUnequipped에서 Visual 제거
                _itemHoldDriver.Unequip();
            }
            else
            {
                DestroyThirdPersonVisual();
            }
        }
        else
        {
            DestroyThirdPersonVisual();
        }

        EquippedItemData = null;
    }


    // =========================================================
    // Use
    // =========================================================

    private void UseItemStarted()
    {
        if (!CanUseItem())
        {
            return;
        }

        // 실제 게임 기능
        _equippedItem?.OnUseStarted();

        // 3인칭 사용 모션
        if (_itemHoldDriver != null &&
            EquippedItemData != null &&
            _itemHoldDriver.IsEquipped)
        {
            _itemHoldDriver.Use(
                EquippedItemData.ThirdPersonUseMotionId
            );
        }
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

        // Visual-only items can still request their configured presentation motion.
        // Gameplay callbacks remain optional and run only when ItemBase exists.
        if (EquippedItemData == null)
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
