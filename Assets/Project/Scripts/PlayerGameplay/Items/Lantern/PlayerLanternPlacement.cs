using System;
using Mirror;
using ProjectShaman.AI;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLanternPlacement : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float _reach = 3f;
    public event Action<long, Ray> PlacementRequested;
    private long _nextRequest, _lastCommitted;
    private bool _busy;
    private float _nextAllowed;
    private PlacementTarget _target;
    public IInteractable Target => _target ??= new PlacementTarget(this);
    private ItemData Equipped => GetComponent<PlayerItemController>()?.EquippedItemData;

    private bool CanAct => isActiveAndEnabled && !_busy && PlayerActionGuard.CanAct(gameObject) &&
        GetComponent<PlayerItemController>() is { CanUseItems: true } &&
        !(GetComponent<PlayerViewModeController>() is { IsBusy: true });
    private bool CanPlace => CanAct && Equipped != null && Equipped.ItemId == "item_lantern";

    public bool TryGetWall(Ray aim, out RaycastHit hit)
    {
        hit = default;
        return CanPlace && TryFindWall(aim, out hit);
    }

    private bool TryFindWall(Ray aim, out RaycastHit hit)
    {
        hit = default;
        if (!CanAct || !Finite(aim.origin) || !Finite(aim.direction) ||
            aim.direction.sqrMagnitude < .99f || Vector3.Distance(aim.origin, transform.position + Vector3.up) > 4f) return false;
        float closest = float.PositiveInfinity;
        float distance = _reach + Vector3.Distance(aim.origin, transform.position + Vector3.up);
        foreach (var candidate in Physics.RaycastAll(aim, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= closest) continue;
            closest = candidate.distance; hit = candidate;
        }
        if (hit.collider == null || Mathf.Abs(hit.normal.y) > .25f ||
            hit.collider.attachedRigidbody != null ||
            hit.collider.GetComponentInParent<IInteractable>() != null ||
            hit.collider.GetComponentInParent<PlayerHealth>() != null ||
            hit.collider.GetComponentInParent<AI_Core>() != null) return false;
        Vector3 origin = transform.position + Vector3.up;
        Vector3 delta = hit.point - origin;
        if (delta.magnitude > _reach) return false;
        foreach (var obstruction in Physics.RaycastAll(origin, delta.normalized, Mathf.Max(0f, delta.magnitude - .02f), ~0, QueryTriggerInteraction.Ignore))
            if (!obstruction.collider.transform.IsChildOf(transform)) return false;
        return true;
    }

    private bool TryLocalRay(out Ray ray)
    {
        ray = default;
        var input = GetComponent<PlayerInputReader>();
        if (!CanPlace || !PlayerLocalPresentation.IsLocal(gameObject) || input == null ||
            !input.isActiveAndEnabled || input.GameplayInputBlocked) return false;
        var interactor = GetComponent<PlayerInteractor>();
        return interactor != null && interactor.TryGetAttackRay(out ray, out _);
    }

    public void RequestPlacement()
    {
        if (Time.unscaledTime < _nextAllowed || !TryLocalRay(out var aim) || !TryGetWall(aim, out _)) return;
        _nextAllowed = Time.unscaledTime + .35f;
        long id = ++_nextRequest;
        if (NetworkClient.active || NetworkServer.active)
        {
            if (PlacementRequested != null) PlacementRequested.Invoke(id, aim);
            else GetComponent<PlayerItemFeedback>()?.Unavailable("초롱불 벽 부착의 네트워크 연결이 필요합니다");
            return;
        }
        TryPlaceAuthoritative(id, aim, () => true, null, null, null, out _);
    }

    // Server adapter validates sender, round and equipped state in authorize.
    // Resolve its registered network prefab, publish spawn + mounted flag, and
    // provide rollback/despawn. The inventory and world change commit together.
    public bool TryPlaceAuthoritative(long requestId, Ray aim, Func<bool> authorize,
        Func<ItemData, GameObject> resolvePrefab, Func<WorldItem, bool> publish,
        Action<WorldItem> rollback, out WorldItem placed, ItemData authoritativeEquippedItem = null)
    {
        placed = null;
        bool online = NetworkClient.active || NetworkServer.active;
        if (requestId <= _lastCommitted || authorize == null || !authorize() ||
            (online && (!NetworkServer.active || resolvePrefab == null || publish == null || rollback == null)) ||
            !TryFindWall(aim, out var hit)) return false;
        // A server's remote player does not own a local held visual. Its adapter
        // supplies NetPlayerEquipment.EquippedItem, never a client-selected asset.
        var item = online ? authoritativeEquippedItem : Equipped;
        if (item == null || item.ItemId != "item_lantern") return false;
        var inventory = GetComponent<PlayerInventory>();
        var prefab = resolvePrefab != null ? resolvePrefab(item) : item.WorldPrefab;
        if (inventory == null || !inventory.Contains(item) || prefab == null) return false;
        var template = prefab.GetComponent<WorldItem>();
        if (template == null || !template.enabled || template.ItemData != item) return false;
        _busy = true;
        GameObject staging = null, created = null;
        WorldItem world = null;
        bool committed = false, attemptedPublish = false;
        try
        {
            staging = new GameObject("LanternPlacementStaging"); staging.SetActive(false);
            created = Instantiate(prefab, staging.transform);
            world = created.GetComponent<WorldItem>();
            created.transform.rotation = Quaternion.LookRotation(hit.normal, Vector3.up);
            var renderers = created.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) if (r.enabled) bounds.Encapsulate(r.bounds);
            float depth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(hit.normal.x), Mathf.Abs(hit.normal.y), Mathf.Abs(hit.normal.z)));
            created.transform.position += hit.point - Vector3.up * bounds.extents.y + hit.normal * (depth + .025f) - bounds.center;
            bounds.center = hit.point - Vector3.up * bounds.extents.y + hit.normal * (depth + .025f);
            // Clearance around corners and adjacent props. The supporting wall is
            // outside this conservative world-aligned box after the depth offset.
            foreach (var obstacle in Physics.OverlapBox(bounds.center, bounds.extents * .98f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform) && !obstacle.transform.IsChildOf(created.transform)) return false;
            var presentation = created.GetComponent<LanternWorldPresentation>();
            if (presentation == null) presentation = created.AddComponent<LanternWorldPresentation>();
            presentation.SetMounted(true);
            committed = inventory.TryDropUnit(item, () =>
            {
                if (!authorize() || (!online && Equipped != item)) return false;
                created.transform.SetParent(null, true); created.SetActive(true);
                attemptedPublish = publish != null;
                return (publish == null || publish(world)) && world != null && world.gameObject.activeInHierarchy;
            });
            if (committed) { _lastCommitted = requestId; placed = world; }
            return committed;
        }
        catch (Exception e) { Debug.LogException(e, this); return false; }
        finally
        {
            _busy = false;
            if (!committed && world != null && attemptedPublish)
                try { rollback?.Invoke(world); } catch (Exception e) { Debug.LogException(e, this); }
            if (!committed && created != null) { created.SetActive(false); Destroy(created); }
            if (staging != null) Destroy(staging);
        }
    }

    private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    private sealed class PlacementTarget : IInteractable
    {
        private readonly PlayerLanternPlacement _owner;
        public PlacementTarget(PlayerLanternPlacement owner) => _owner = owner;
        public string InteractionPrompt => "초롱불 벽에 걸기";
        public bool CanInteract(GameObject interactor) => _owner != null && interactor == _owner.gameObject &&
            _owner.TryLocalRay(out var ray) && _owner.TryGetWall(ray, out _);
        public void Interact(GameObject interactor) { if (CanInteract(interactor)) _owner.RequestPlacement(); }
    }
}
