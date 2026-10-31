using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectShaman.AI;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.World;

// Separate secondary-use request: never send this through ItemUseRequested, which
// the current network adapter interprets as self-protection. No client-side spend.
[DisallowMultipleComponent]
public sealed class PlayerHouseBarrierPlacement : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _reach = 3f;
    [SerializeField] private LayerMask _surfaceMask = ~0;
    public event Action<ItemUseContext> PlacementRequested;
    public event Action<HouseBarrierWard> PlacementConfirmed;
    private PlayerInputReader _input;
    private PlayerItemController _items;
    private InputManager _inputManager;
    private bool _placing;
    private float _nextPlacement;
    private readonly List<PlaceArea> _homes = new();

    private void Awake()
    {
        _input = GetComponent<PlayerInputReader>();
        _items = GetComponent<PlayerItemController>();
    }
    private void Update()
    {
        if (!PlayerLocalPresentation.IsLocal(gameObject)) return;
        if (_inputManager == null) _inputManager = FindFirstObjectByType<InputManager>();
        // Player-owned fallback until InputManager exposes SecondaryUse. The shared
        // Player action map must be enabled, so rebinding/spectator input cannot place.
        if (_input == null || !_input.isActiveAndEnabled ||
            _input.GameplayInputBlocked || !PlayerActionGuard.CanAct(gameObject) ||
            _inputManager == null || !_inputManager.Actions.Player.enabled ||
            Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame) return;
        RequestPlacement();
    }

    public void RequestPlacement()
    {
        if (_items == null || !_items.CanUseItems || !PlayerActionGuard.CanAct(gameObject) ||
            !(_items.EquippedItemData is TalismanItemData data) || data.TalismanType != TalismanType.Barrier ||
            GetComponent<PlayerViewModeController>() is { IsBusy: true } ||
            (_input != null && _input.GameplayInputBlocked) ||
            GetComponent<PlayerNotebookUI>() is { IsOpen: true } ||
            (UIManager.Existing != null && UIManager.Existing.IsMenuOpen)) return;
        var camera = GetComponentInChildren<Camera>();
        if (camera == null) return;
        var context = new ItemUseContext(gameObject, data, camera.transform.position, camera.transform.forward);
        if (!HouseBarrierWard.HasRunningClock) { Unavailable("게임 시간이 시작된 뒤 부착할 수 있습니다"); return; }
        if (!TryFindHome(context, out _, out _, out var reason)) { Unavailable(reason); return; }
        if (NetworkClient.active || NetworkServer.active)
        {
            if (PlacementRequested == null) Unavailable("집 결계의 네트워크 연결이 필요합니다");
            else PlacementRequested.Invoke(context);
            return;
        }
        var inventory = GetComponent<PlayerInventory>();
        if (TryPlaceAuthoritative(context, () => BarrierTalismanUseAction.IsEquipped(gameObject, data),
            () => inventory != null && inventory.TryExchangePersonalItems(
                new Dictionary<ItemData, int> { [data] = 1 }, null, 0, null, null),
            ward => true, ward => Destroy(ward.gameObject), out var placed))
        {
            PlayerItemActions.ReportUse(gameObject, data);
            Debug.Log($"[HouseBarrier] {placed.Home.DisplayName} 보호: 다음 날 아침까지", this);
        }
    }

    // Server adapter must validate the sender, equipped item and round before calling.
    // registerSpawn creates presentation only; the ward is armed only after atomic spend.
    // On true the adapter reports use exactly once. On false no success is reported.
    public bool TryPlaceAuthoritative(in ItemUseContext context, Func<bool> isEquipped,
        Func<bool> tryConsumeOne, Func<HouseBarrierWard, bool> registerSpawn,
        Action<HouseBarrierWard> despawn, out HouseBarrierWard ward)
    {
        ward = null;
        if (_placing || context.Owner != gameObject || !ItemAttackAuthority.CanResolve(gameObject) ||
            Time.time < _nextPlacement || !HouseBarrierWard.HasRunningClock ||
            !(context.Data is TalismanItemData data) || data.TalismanType != TalismanType.Barrier ||
            !(data.UseAction is BarrierTalismanUseAction) || isEquipped == null || tryConsumeOne == null ||
            registerSpawn == null || despawn == null) return false;
        var inventory = GetComponent<PlayerInventory>();
        if (inventory == null || !inventory.HasItem(data, 1) || !isEquipped() ||
            !TryFindHome(context, out var home, out var hit, out _)) return false;
        _placing = true;
        HouseBarrierWard created = null;
        bool committed = false;
        try
        {
            created = new GameObject("HouseBarrier_" + home.PlaceId).AddComponent<HouseBarrierWard>();
            created.Prepare(home, hit.point, hit.normal, data.PaperTexture, despawn);
            if (!registerSpawn(created) || created == null || !created.gameObject.activeInHierarchy ||
                home == null || !home.isActiveAndEnabled || home.OwnerId != created.ResidentId ||
                !ItemAttackAuthority.CanResolve(gameObject) || !isEquipped() || HouseBarrierWard.IsProtecting(home) ||
                !HouseBarrierWard.HasRunningClock || !tryConsumeOne()) return false;
            created.Arm();
            _nextPlacement = Time.time + .6f;
            ward = created;
            committed = true;
            try { PlacementConfirmed?.Invoke(created); } catch (Exception e) { Debug.LogException(e, this); }
            return true;
        }
        catch (Exception e) { Debug.LogException(e, this); return false; }
        finally
        {
            _placing = false;
            if (!committed && created != null) created.Expire();
        }
    }

    public bool TryFindHome(in ItemUseContext context, out PlaceArea home, out RaycastHit hit, out string reason)
    {
        home = null; hit = default; reason = "주민 집 구역의 벽이나 바닥을 바라보세요";
        if (!Finite(context.AimOrigin) || !Finite(context.AimDirection) || context.AimDirection.sqrMagnitude < .99f ||
            Vector3.Distance(transform.position, context.AimOrigin) > 4f || !float.IsFinite(_reach) || _reach <= 0f) return false;
        float closest = float.PositiveInfinity;
        foreach (var candidate in Physics.RaycastAll(context.AimOrigin, context.AimDirection, _reach, _surfaceMask, QueryTriggerInteraction.Ignore))
        {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= closest) continue;
            closest = candidate.distance; hit = candidate;
        }
        if (hit.collider == null || hit.collider.GetComponentInParent<PlayerHealth>() != null ||
            hit.collider.GetComponentInParent<AI_Core>() != null) return false;
        _homes.Clear(); PlaceRegistry.CollectByType(PlaceType.Home, _homes);
        foreach (var candidate in _homes)
        {
            if (!candidate.isActiveAndEnabled || string.IsNullOrEmpty(candidate.OwnerId) ||
                !MatchesHomeSurface(candidate, hit)) continue;
            if (home == null || candidate.Depth > home.Depth || (candidate.Depth == home.Depth && candidate.Footprint < home.Footprint)) home = candidate;
        }
        if (home == null) return false;
        if (HouseBarrierWard.IsProtecting(home)) { reason = "이미 결계로 보호 중인 집입니다"; return false; }
        return true;
    }

    // Home markers can describe a resident's standing point (radius 1m), rather than
    // the house footprint. Resolve walls through the floor below that registered point.
    // Never select the nearest home across the village or enlarge AI-owned home areas.
    private bool MatchesHomeSurface(PlaceArea home, RaycastHit surface)
    {
        if (home.Contains(surface.point)) return true;
        var floors = Physics.RaycastAll(home.Center + Vector3.up * .25f, Vector3.down, 6f,
            ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(floors, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var floor in floors)
        {
            if (floor.collider.GetComponentInParent<PlayerHealth>() != null ||
                floor.collider.GetComponentInParent<AI_Core>() != null ||
                floor.collider.GetComponentInParent<WorldItem>() != null) continue;
            if (floor.collider is TerrainCollider) return false;
            if (floor.normal.y < .5f) continue;
            var building = floor.transform.parent;
            if (building == null || !surface.transform.IsChildOf(building)) continue;

            // A scene/village container is not a building. Reject large common roots
            // instead of accepting a neighbour's wall through a shared parent.
            bool found = false;
            Bounds bounds = default;
            foreach (var collider in building.GetComponentsInChildren<Collider>())
            {
                if (!collider.enabled || collider.isTrigger) continue;
                if (collider is TerrainCollider) return false;
                if (!found) { bounds = collider.bounds; found = true; }
                else bounds.Encapsulate(collider.bounds);
            }
            if (!found) return false;
            if (bounds.size.x <= 30f && bounds.size.z <= 30f) return true;

            // Some house assets contain oversized decorative colliders. A large bounds
            // alone must not invalidate their real walls, but a shared village root must.
            if (Vector3.Distance(floor.point, surface.point) > 15f) return false;
            foreach (var other in _homes)
            {
                if (other == home || !other.isActiveAndEnabled) continue;
                foreach (var support in Physics.RaycastAll(other.Center + Vector3.up * .25f,
                    Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!(support.collider is TerrainCollider) && support.normal.y > .5f &&
                        support.transform.IsChildOf(building)) return false;
                }
            }
            return true;
        }
        return false;
    }
    private void Unavailable(string message) => GetComponent<PlayerItemFeedback>()?.Unavailable(message);
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
