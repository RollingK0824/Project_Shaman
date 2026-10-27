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
        return CanPlace && TryFindWall(aim, out hit, out _);
    }

    private bool TryFindWall(Ray aim, out RaycastHit hit, out MeshFilter[] visualSurface)
    {
        hit = default;
        visualSurface = null;
        if (!CanAct || !Finite(aim.origin) || !Finite(aim.direction) ||
            aim.direction.sqrMagnitude < .99f || Vector3.Distance(aim.origin, transform.position + Vector3.up) > 4f) return false;
        float closest = float.PositiveInfinity;
        float distance = _reach + Vector3.Distance(aim.origin, transform.position + Vector3.up);
        foreach (var candidate in RaycastBothSides(aim, distance))
        {
            if (candidate.collider.transform.IsChildOf(transform) || candidate.distance >= closest) continue;
            closest = candidate.distance; hit = candidate;
        }
        // Some environment collision meshes have inward-facing triangles. Always use
        // the nearest surface and orient its normal toward the viewing side.
        if (Vector3.Dot(hit.normal, aim.direction) > 0f) hit.normal = -hit.normal;
        if (hit.collider == null || Mathf.Abs(hit.normal.y) > .25f || Vector3.Dot(hit.normal, aim.direction) >= -.001f ||
            hit.collider.attachedRigidbody != null ||
            hit.collider.GetComponentInParent<IInteractable>() != null ||
            hit.collider.GetComponentInParent<PlayerHealth>() != null ||
            hit.collider.GetComponentInParent<AI_Core>() != null) return false;
        bool refined = LanternMountSurface.TryRefine(hit.collider, aim, distance, out visualSurface, out var point, out var normal);
        if (visualSurface != null)
        {
            if (!refined) return false; // The coarse hull was hit, but the visible object was missed.
            hit.point = point; hit.normal = normal;
            hit.distance = Vector3.Distance(aim.origin, point);
            if (Mathf.Abs(normal.y) > .25f) return false;
        }
        Vector3 origin = transform.position + Vector3.up;
        Vector3 delta = hit.point - origin;
        if (delta.magnitude > _reach) return false;
        foreach (var obstruction in RaycastBothSides(new Ray(origin, delta.normalized), Mathf.Max(0f, delta.magnitude - .02f)))
            if (!obstruction.collider.transform.IsChildOf(transform) &&
                !(visualSurface != null && obstruction.collider == hit.collider)) return false;
        if (visualSurface != null && LanternMountSurface.Raycast(visualSurface, new Ray(origin, delta.normalized),
                Mathf.Max(0f, delta.magnitude - .02f), out _, out _)) return false;
        return true;
    }

    private static RaycastHit[] RaycastBothSides(Ray ray, float distance)
    {
        // Scope this global Unity query option to the synchronous query only.
        // AI, weapons and other callers retain their original setting.
        bool previous = Physics.queriesHitBackfaces;
        try
        {
            Physics.queriesHitBackfaces = true;
            return Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore);
        }
        finally { Physics.queriesHitBackfaces = previous; }
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
        if (!TryPlaceAuthoritative(id, aim, () => true, null, null, null, out _))
            GetComponent<PlayerItemFeedback>()?.Unavailable("이 위치에는 초롱불을 걸 수 없습니다. 위치를 조금 옮겨주세요");
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
            !TryFindWall(aim, out var hit, out var visualSurface)) return false;
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
            // Keep the lantern upright; its back faces the surface, its front the player.
            var rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized, Vector3.up);
            created.transform.rotation = rotation;
            if (!TryGetPlacementBounds(created.transform, out var bounds)) return false;
            Vector3 localNormal = Quaternion.Inverse(rotation) * hit.normal;
            float depth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z)));
            // Offset horizontally so the handle stays at the aimed height even on slightly sloped walls.
            float offset = (depth + .005f + hit.normal.y * bounds.extents.y) / localNormal.z;
            Vector3 center = hit.point - Vector3.up * bounds.extents.y + rotation * Vector3.forward * offset;
            created.transform.position = center - rotation * bounds.center;
            // Use the same oriented box as the rendered model, including on oblique pillars.
            // Local renderer geometry avoids dependence on inactive world bounds and inflated world AABBs.
            // A coarse support hull can extend well beyond its visible wood. In that
            // case validate the actual triangles instead of rejecting that hull overlap.
            if (visualSurface != null && LanternMountSurface.Overlaps(visualSurface, center, bounds.extents, rotation)) return false;
            foreach (var obstacle in Physics.OverlapBox(center, bounds.extents, rotation, ~0, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform) && !obstacle.transform.IsChildOf(created.transform) &&
                    !(visualSurface != null && obstacle == hit.collider)) return false;
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

    private static bool TryGetPlacementBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        // Rotation-only frame keeps prefab/child scale in world units without requiring activation.
        Matrix4x4 toFrame = Matrix4x4.TRS(root.position, root.rotation, Vector3.one).inverse;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
            bool visible = true;
            for (var parent = renderer.transform; parent != root; parent = parent.parent)
                if (!parent.gameObject.activeSelf) { visible = false; break; }
            if (!visible) continue;
            var local = renderer.localBounds;
            var matrix = toFrame * renderer.localToWorldMatrix;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(local.center + Vector3.Scale(local.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                if (!Finite(point)) return false;
                if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                else bounds.Encapsulate(point);
            }
        }
        return found && bounds.size.x > .001f && bounds.size.y > .001f && bounds.size.z > .001f;
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
