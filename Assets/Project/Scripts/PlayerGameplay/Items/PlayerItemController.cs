using UnityEngine;
using ProjectShaman.ItemHold;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerInventory))]
public class PlayerItemController : MonoBehaviour
{
    private bool _firstPersonPresentationSuppressed;
    public void SetFirstPersonPresentationSuppressed(bool suppressed) => _firstPersonPresentationSuppressed = suppressed;
    [Header("References")]
    [SerializeField] private Transform _itemHoldPoint;


    [Header("Third Person Hold")]
    [SerializeField] private ItemHoldDriver _itemHoldDriver;

    public int SelectedSlotIndex { get; private set; } = -1;

    public ItemData EquippedItemData { get; private set; }
    public string EquippedTalismanInstanceId { get; private set; }

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
    private bool _remotePresentation;
    private ItemData _remoteHeldItem;

    // 아이템 교체 시
    // 기존 아이템 Unequip이 끝난 뒤 장착할 아이템
    private ItemData _pendingThirdPersonItem;
    private readonly System.Collections.Generic.Dictionary<ItemData, float> _useCooldowns = new();
    public float RemainingUseCooldown(ItemData item) => item != null && _useCooldowns.TryGetValue(item, out float until)
        ? Mathf.Max(0f, until - Time.time) : 0f;
    // Local request presentation only. This never reports quest success or resolves an attack.
    public void PlayUsePresentation(ItemData data)
    {
        if (data == null || data != EquippedItemData || !CanUseItems ||
            !PlayerActionGuard.CanAct(gameObject)) return;
        GetComponent<PlayerItemFeedback>()?.Play(data);
        if (_itemHoldDriver != null && _itemHoldDriver.IsEquipped)
            _itemHoldDriver.Use(data.ThirdPersonUseMotionId);
    }

    public bool TryBeginUseCooldown(ItemData item, float seconds)
    {
        if (item == null || !CanUseItems || !PlayerActionGuard.CanAct(gameObject)) return false;
        if (_useCooldowns.TryGetValue(item, out float until) && Time.time < until) return false;
        _useCooldowns[item] = Time.time + Mathf.Max(0f, seconds);
        return true;
    }


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

    }


    private void OnEnable()
    {
        _inputReader.SlotSelected += SelectSlot;
        _inventory.QuickSlotChanged += HandleQuickSlotChanged;

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
        UnequipCurrentItem();
        DestroyThirdPersonVisual();
        if (_inventory != null) _inventory.QuickSlotChanged -= HandleQuickSlotChanged;
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
        // Presentation visibility never disables the equipped gameplay component.
        bool firstPerson = _viewModeController == null ||
            _viewModeController.CurrentViewMode == PlayerViewModeController.ViewMode.FirstPerson;
        bool local = !_remotePresentation && PlayerLocalPresentation.IsLocal(gameObject);
        SetVisualVisibility(_firstPersonRenderers, firstPerson && local && !_firstPersonPresentationSuppressed);
        SetVisualVisibility(_thirdPersonRenderers, PlayerActionGuard.CanAct(gameObject) && (!local || !firstPerson));
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
        if (!CanUseItems || !PlayerActionGuard.CanAct(gameObject))
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

    private void HandleQuickSlotChanged(int slotIndex, ItemData item)
    {
        if (slotIndex != SelectedSlotIndex) return;
        if (item == null) UnequipCurrentItem();
        else EquipItem(item);
    }

    private void EquipItem(ItemData itemData)
    {
        if (!CanUseItems || !PlayerActionGuard.CanAct(gameObject) ||
            itemData == null || !itemData.CanUseQuickSlot)
        {
            return;
        }

        _remotePresentation = false;
        _remoteHeldItem = null;
        string instanceId = _inventory.GetQuickSlotItem(SelectedSlotIndex) == itemData
            ? _inventory.GetQuickSlotInstanceId(SelectedSlotIndex) : null;
        if (ReferenceEquals(EquippedItemData, itemData) && EquippedTalismanInstanceId == instanceId)
        {
            return;
        }

        // 기존 1인칭 아이템만 먼저 해제한다.
        UnequipFirstPerson();

        // 새로운 아이템을 현재 장착 데이터로 지정
        EquippedItemData = itemData;
        EquippedTalismanInstanceId = instanceId;

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
        if (_itemHoldPoint == null)
        {
            var camera = GetComponentInChildren<Camera>(true);
            if (camera != null)
            {
                _itemHoldPoint = camera.transform.Find("FirstPersonItemAnchor");
                if (_itemHoldPoint == null)
                {
                    _itemHoldPoint = new GameObject("FirstPersonItemAnchor").transform;
                    _itemHoldPoint.SetParent(camera.transform, false);
                    _itemHoldPoint.localPosition = new Vector3(.26f, -.17f, .62f);
                }
            }
        }
        if (_itemHoldPoint == null || itemData.VisualPrefab == null)
        {
            Debug.LogWarning($"[Item] {itemData.DisplayName}: Item Anchor or Visual Prefab is missing.", this);
            return;
        }

        // A presentation parent keeps viewport fitting separate from item use animations.
        _equippedObject = new GameObject("FirstPersonItemPresentation");
        _equippedObject.transform.SetParent(_itemHoldPoint, false);
        var motion = new GameObject("UseMotion").transform;
        motion.SetParent(_equippedObject.transform, false);
        var visual = Instantiate(itemData.VisualPrefab, motion, false).transform;
        visual.localPosition = Vector3.zero;
        visual.localRotation = Quaternion.Euler(itemData.FirstPersonLocalEuler);
        // Preserve the prefab's imported orientation/scale underneath its root.
        visual.localScale = Vector3.one;
        _firstPersonRenderers = _equippedObject.GetComponentsInChildren<Renderer>(true);
        if (TryGetMeshBounds(_firstPersonRenderers, _itemHoldPoint, out Bounds bounds))
        {
            float longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            float scale = itemData.FirstPersonDisplaySize > 0f && longest > .00001f
                ? itemData.FirstPersonDisplaySize / longest : 1f;
            visual.localScale = Vector3.Scale(Vector3.one * scale, itemData.FirstPersonLocalScale);
            if (TryGetMeshBounds(_firstPersonRenderers, _itemHoldPoint, out bounds))
                visual.localPosition = itemData.FirstPersonLocalPosition - bounds.center;
        }
        else
        {
            visual.localScale = itemData.FirstPersonLocalScale;
            visual.localPosition = itemData.FirstPersonLocalPosition;
        }
        foreach (var col in _equippedObject.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var rb in _equippedObject.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; }
        _equippedItem = _equippedObject.GetComponentInChildren<ItemBase>(true);
        InitializeEquippedItem(itemData);
        foreach (var component in _equippedObject.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IItemVisualContext context) context.BindItemVisual(itemData, gameObject);
        var layout = _equippedObject.AddComponent<FirstPersonItemLayout>();
        layout.Bind(_itemHoldPoint.GetComponentInParent<Camera>(), itemData, _firstPersonRenderers);
        GetComponent<PlayerItemFeedback>()?.Bind(motion, itemData);
    }

    // Measure actual geometry, excluding particle/trail bounds that grow during play.
    private static bool TryGetMeshBounds(Renderer[] renderers, Transform relativeTo, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var renderer in renderers)
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = relativeTo.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int i = 0; i < 8; i++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        return found;
    }

    private void UnequipFirstPerson()
    {
        // Clear references before callbacks: consuming an item can notify inventory listeners.
        var item = _equippedItem;
        var visual = _equippedObject;
        _equippedItem = null;
        _equippedObject = null;
        _firstPersonRenderers = null;
        item?.OnUseCanceled();
        item?.OnUnequipped();
        if (visual != null) { visual.SetActive(false); Destroy(visual); }
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
        PlayerItemFeedback.PrepareVisual(_thirdPersonVisual, itemData);

        visualTransform.localPosition =
            itemData.ThirdPersonLocalPosition;

        visualTransform.localRotation =
            Quaternion.Euler(
                itemData.ThirdPersonLocalEuler
            );

        visualTransform.localScale =
            itemData.ThirdPersonLocalScale;
        if (TryGetMeshBounds(_thirdPersonVisual.GetComponentsInChildren<Renderer>(true), visualTransform, out var gripBounds))
            _thirdPersonVisual.AddComponent<ThirdPersonItemGrip>().Bind(_itemHoldDriver.Animator, itemData, gripBounds);
        var mirrorView = _thirdPersonVisual.GetComponentInChildren<MirrorObservationView>(true);
        if (mirrorView != null)
        {
            mirrorView.enabled = false;
            var reflection = _thirdPersonVisual.GetComponent<ThirdPersonMirrorReflection>();
            if (reflection == null) reflection = _thirdPersonVisual.AddComponent<ThirdPersonMirrorReflection>();
            reflection.Bind(mirrorView.Surface);
        }
        _thirdPersonRenderers = _thirdPersonVisual.GetComponentsInChildren<Renderer>(true);
        foreach (var component in _thirdPersonVisual.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IItemVisualContext context) context.BindItemVisual(itemData, gameObject);

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

        _thirdPersonVisual.SetActive(false);
        Destroy(_thirdPersonVisual);

        _thirdPersonVisual = null;
        _thirdPersonRenderers = null;
    }

    // One world-space spray for either the local third-person view or the existing remote RPC.
    // Damage, consumption and success events remain owned by SaltUseAction.Execute.
    public bool PlayThirdPersonSaltSpray(Vector3 direction)
    {
        if (_thirdPersonVisual == null || !PlayerActionGuard.CanAct(gameObject)) return false;
        var template = _thirdPersonVisual.GetComponentInChildren<ParticleSystem>(true);
        if (template == null) return false;
        var spray = Instantiate(template, template.transform.position, Quaternion.LookRotation(direction.sqrMagnitude > .001f ? direction : transform.forward));
        spray.name = "ThirdPersonSaltSpray";
        spray.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        spray.transform.localScale = Vector3.one;
        var main = spray.main;
        main.loop = false; main.playOnAwake = false; main.duration = .15f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.45f, .85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(.006f, .013f);
        main.startColor = new Color(.95f,.93f,.85f,1f); main.gravityModifier = .7f;
        var emission = spray.emission; emission.enabled = true; emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 65) });
        var shape = spray.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 16f; shape.radius = .018f;
        spray.GetComponent<ParticleSystemRenderer>().enabled = true;
        spray.Play(true);
        Destroy(spray.gameObject, 1.6f);
        return true;
    }

    // [원격 사본 전용] 네트워크가 확정한 장착 아이템의 3인칭 모델만 교체
    // 팔 자세는 NetworkNaimator가 동기화함. ItemHoldDriver는 구동하지 않음
    public void ApplyRemoteHeldVisual(ItemData itemData)
    {
        _remotePresentation = true;
        if (!PlayerActionGuard.CanAct(gameObject)) itemData = null;
        if (_remoteHeldItem == itemData && (_thirdPersonVisual != null || itemData == null)) return;
        _remoteHeldItem = itemData;
        DestroyThirdPersonVisual();

        if (itemData == null || itemData.HoldType == HoldType.Empty)
        {
            return;
        }

        SpawnThirdPersonVisual(itemData);
    }


    // =========================================================
    // Unequip
    // =========================================================

    public void ClearHeldVisualsOnDeath()
    {
        _remoteHeldItem = null;
        CanUseItems = false;
        UnequipCurrentItem();
        DestroyThirdPersonVisual();
    }

    public void UnequipCurrentItem()
    {
        _pendingThirdPersonItem = null;
        EquippedItemData = null;
        EquippedTalismanInstanceId = null;
        UnequipFirstPerson();
        DestroyThirdPersonVisual();
        if (_itemHoldDriver != null && _itemHoldDriver.Phase != HoldPhase.Empty)
            _itemHoldDriver.Unequip();
        else DestroyThirdPersonVisual();
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
        if (EquippedItemData != null && EquippedItemData.UseAction == null && _itemHoldDriver != null &&
            _itemHoldDriver.IsEquipped)
        {
            _itemHoldDriver.Use(
                EquippedItemData.ThirdPersonUseMotionId
            );
        }
    }


    private void UseItemCanceled()
    {
        if (_equippedItem == null)
        {
            return;
        }

        _equippedItem.OnUseCanceled();
    }


    private bool CanUseItem()
    {
        if (!CanUseItems || !PlayerActionGuard.CanAct(gameObject))
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
