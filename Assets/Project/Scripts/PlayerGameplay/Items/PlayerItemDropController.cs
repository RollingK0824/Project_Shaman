using System;
using Mirror;
using UnityEngine;

// Local request / authoritative commit only. No Commands, RPCs or network prefab changes.
[DisallowMultipleComponent]
public sealed class PlayerItemDropController : MonoBehaviour
{
    [SerializeField, Range(1f, 1.5f)] private float _forwardDistance = 1.25f;
    [SerializeField] private LayerMask _groundMask = ~0;
    [SerializeField] private LayerMask _obstacleMask = ~0;
    private PlayerInventory _inventory;
    private Func<bool> _authority;
    private Func<WorldItem, bool> _publishWorld;
    private Action<WorldItem> _rollbackWorld;
    private Func<ItemData, GameObject> _resolvePrefab;
    private Func<ItemData, bool> _isCarriedWorldObject;
    private long _nextRequest, _lastFinalRequest, _pendingRequest;
    private bool _busy;
    public event Action<long, ItemData> DropRequested;
    public event Action<long, string> DrawingDropRequested;
    public event Action<long, bool, string> DropResolved;
    public bool IsPending => _pendingRequest != 0;
    public string LastMessage { get; private set; }
    private void Awake() => _inventory = GetComponent<PlayerInventory>();
    public void SetAuthorityCheck(Func<bool> check) => _authority = check;
    // Server adapter must publish/despawn using its own NetworkIdentity/registry rules.
    public void SetWorldPublisher(Func<WorldItem, bool> publish, Action<WorldItem> rollback)
    {
        if ((publish == null) != (rollback == null)) throw new ArgumentException("World publisher and rollback must be set together.");
        _publishWorld = publish; _rollbackWorld = rollback;
    }

    public void SetWorldPrefabResolver(Func<ItemData, GameObject> resolve) => _resolvePrefab = resolve;
    // An item whose world object is already carried along (a villager's tool): dropping only takes it out of the
    // inventory, and the carried object puts itself down, so no new world item is made
    public void SetCarriedWorldObjectCheck(Func<ItemData, bool> isCarried) => _isCarriedWorldObject = isCarried;

    private bool CanCommit => isActiveAndEnabled && PlayerActionGuard.CanAct(gameObject) &&
        (!NetworkClient.active && !NetworkServer.active ? _authority == null || _authority() :
         NetworkServer.active && _authority != null && _authority() && _publishWorld != null && _rollbackWorld != null);

    public bool CanDrop(ItemData item, out string reason)
    {
        reason = null;
        if (item == null) reason = "아이템을 선택하세요.";
        else if (item.Category == ItemCategory.Material) reason = "공용 재료는 버릴 수 없습니다.";
        else if (item.ItemId == "item_notebook") reason = "수첩은 버릴 수 없습니다.";
        else if (item.PreventDrop) reason = "기본 지급 아이템은 버릴 수 없습니다.";
        else if (_inventory == null || !_inventory.Contains(item)) reason = "보유한 아이템이 없습니다.";
        else if (item.WorldPrefab == null) reason = "월드 프리팹이 없어 버릴 수 없습니다.";
        else if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject)) reason = "지금은 버릴 수 없습니다.";
        else if (_busy || IsPending) reason = "버리기 결과를 기다리고 있습니다.";
        return reason == null;
    }

    public bool RequestDrop(ItemData item)
    {
        if (!CanDrop(item, out var reason)) { LastMessage = reason; return false; }
        long request = ++_nextRequest;
        if (CanCommit) return TryDropFinal(request, item, out _);
        if (DropRequested == null) { LastMessage = "서버 버리기 연결이 필요합니다."; return false; }
        _pendingRequest = request; LastMessage = "서버 확인을 기다리고 있습니다.";
        try { DropRequested.Invoke(request, item); }
        catch (Exception e) { _pendingRequest = 0; Debug.LogException(e, this); LastMessage = "버리기 요청을 보내지 못했습니다."; }
        return false; // Pending never means a local ownership change.
    }

    public bool RequestDrawingDrop(string id)
    {
        var collection = GetComponent<PlayerTalismanCollection>();
        if (collection == null || collection.Find(id, out var item) == null || item.WorldPrefab == null ||
            _busy || IsPending || !PlayerActionGuard.CanAct(gameObject)) { LastMessage = "부적을 버릴 수 없습니다."; return false; }
        long request = ++_nextRequest;
        if (CanCommit) return TryDropDrawingFinal(request, id, out _);
        if (DrawingDropRequested == null) { LastMessage = "서버 버리기 연결이 필요합니다."; return false; }
        _pendingRequest = request; LastMessage = "서버 확인을 기다리고 있습니다.";
        try { DrawingDropRequested.Invoke(request, id); }
        catch (Exception e) { _pendingRequest = 0; Debug.LogException(e, this); LastMessage = "버리기 요청을 보내지 못했습니다."; }
        return false;
    }

    public bool TryDropDrawingFinal(long requestId, string id, out WorldItem dropped)
    {
        var collection = GetComponent<PlayerTalismanCollection>();
        var drawing = collection != null ? collection.Find(id, out _) : null;
        if (drawing == null) { dropped = null; LastMessage = "보유한 부적이 없습니다."; return false; }
        collection.Find(id, out var type);
        return TryDropFinalInternal(requestId, type, id, out dropped);
    }
    // Adapter validates sender/ownership/catalogue first. Request IDs are increasing per player session.
    // Server computes the position; never accept a client-authored position or prefab.
    public bool TryDropFinal(long requestId, ItemData item, out WorldItem dropped)
        => TryDropFinalInternal(requestId, item, null, out dropped);

    private bool TryDropFinalInternal(long requestId, ItemData item, string drawingId, out WorldItem dropped)
    {
        dropped = null;
        if (!CanCommit || requestId <= _lastFinalRequest || requestId <= 0 || _busy) return false;
        if (_pendingRequest == requestId) _pendingRequest = 0;
        if (drawingId == null && !CanDrop(item, out var reason)) { LastMessage = reason; return false; }
        if (drawingId != null && (item == null || item.WorldPrefab == null || !PlayerActionGuard.CanAct(gameObject))) return false;
        _lastFinalRequest = requestId; _nextRequest = Math.Max(_nextRequest, requestId);
        if (drawingId == null && _isCarriedWorldObject != null && _isCarriedWorldObject(item))
        {
            bool removed = _inventory.TryDropUnit(item, () => true);
            LastMessage = removed ? "아이템 1개를 바닥에 내려놓았습니다." : "버리지 못했습니다. 소지품은 유지됩니다.";
            return removed;
        }
        _busy = true;
        GameObject staging = null, instance = null;
        WorldItem world = null; bool published = false, committed = false;
        try
        {
            GameObject prefab = _resolvePrefab != null ? _resolvePrefab(item) : null;
            if (prefab == null) prefab = item.WorldPrefab;
            // Capture before TryDropUnit can unequip and destroy the held visual.
            Vector3 releasePoint = GetDropReleasePoint(item);

            var prototype = prefab.GetComponent<WorldItem>();
            if (!prefab.activeSelf || prototype == null || !prototype.enabled || prototype.ItemData != item ||
                prefab.GetComponentsInChildren<WorldItem>(true).Length != 1)
            { LastMessage = "월드 아이템 연결을 확인해 주세요."; return false; }
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .5f) return false;
            Vector3 start = transform.position + Vector3.up;
            Vector3 above = start + forward * _forwardDistance;
            foreach (var obstacle in Physics.OverlapSphere(start, .15f, _obstacleMask, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(transform)) { LastMessage = "앞이 막혀 있어 버릴 수 없습니다."; return false; }
            foreach (var hit in Physics.SphereCastAll(start, .15f, forward, _forwardDistance, _obstacleMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform)) { LastMessage = "앞이 막혀 있어 버릴 수 없습니다."; return false; }
            RaycastHit? ground = null;
            foreach (var hit in Physics.RaycastAll(above + Vector3.up, Vector3.down, 4f, _groundMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.normal.y >= .65f && (!ground.HasValue || hit.distance < ground.Value.distance)) ground = hit;
            if (!ground.HasValue) { LastMessage = "안전한 바닥을 찾지 못했습니다."; return false; }
            staging = new GameObject("Drop staging"); staging.SetActive(false);
            instance = Instantiate(prefab, ground.Value.point, Quaternion.LookRotation(forward), staging.transform);
            world = instance.GetComponent<WorldItem>();
            if (!TryBounds(instance, out var bounds)) { LastMessage = "아이템 크기를 확인할 수 없습니다."; return false; }
            Vector3 shift = Vector3.up * (ground.Value.point.y - bounds.min.y + .035f);
            instance.transform.position += shift; bounds.center += shift;
            if (bounds.size.magnitude > 4f) { LastMessage = "아이템 크기가 너무 큽니다."; return false; }
            Physics.SyncTransforms();
            foreach (var obstacle in Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * .01f, Quaternion.identity, _obstacleMask, QueryTriggerInteraction.Ignore))
                if (!obstacle.transform.IsChildOf(instance.transform)) { LastMessage = "아이템을 놓을 공간이 부족합니다."; return false; }
            // Sweep the whole item from the player's side to its destination, preventing thin-wall placement.
            Vector3 from = new Vector3(transform.position.x, bounds.center.y, transform.position.z);
            Vector3 travel = bounds.center - from;
            foreach (var hit in Physics.BoxCastAll(from, bounds.extents, travel.normalized, Quaternion.identity, travel.magnitude,
                _obstacleMask, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(instance.transform))
                { LastMessage = "앞이 막혀 있어 버릴 수 없습니다."; return false; }
            bool Publish()
            {
                instance.transform.SetParent(null, true);
                if (!world.isActiveAndEnabled) return false;
                if (_publishWorld != null)
                {
                    published = true; // Rollback must also cover a publisher that partially failed.
                    if (!_publishWorld(world)) return false;
                }
                return world != null && world.isActiveAndEnabled;
            }
            if (drawingId == null) committed = _inventory.TryDropUnit(item, Publish);
            else committed = GetComponent<PlayerTalismanCollection>().TryDropDrawing(drawingId, data =>
            {
                world.SetDrawing(data);
                return Publish();
            });
            if (!committed) { LastMessage = "버리지 못했습니다. 소지품은 유지됩니다."; return false; }
            // Presentation failure must never turn an already committed drop into a failed transaction.
            ItemDropPresentation presentation = null;
            try
            {
                presentation = world.GetComponent<ItemDropPresentation>();
                if (presentation == null) presentation = world.gameObject.AddComponent<ItemDropPresentation>();
                presentation.Play(releasePoint);
            }
            catch (Exception e)
            {
                if (presentation != null) presentation.enabled = false;
                Debug.LogException(e, world);
            }
            dropped = world; LastMessage = "아이템 1개를 바닥에 내려놓았습니다.";
            return true;
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            LastMessage = "버리지 못했습니다. 소지품은 유지됩니다.";
            return false;
        }
        finally
        {
            if (!committed && instance != null)
            {
                if (published) { try { _rollbackWorld?.Invoke(world); } catch (Exception e) { Debug.LogException(e, this); } }
                if (instance != null) { instance.SetActive(false); Destroy(instance); }
            }
            if (staging != null) Destroy(staging);
            _busy = false;
            try { DropResolved?.Invoke(requestId, committed, LastMessage); } catch (Exception e) { Debug.LogException(e, this); }
        }
    }
    // Response only clears UI pending state. Inventory/equipment arrive through existing replication.
    public void ApplyDropResult(long requestId, bool success, string message)
    {
        if (requestId != _pendingRequest) return;
        _pendingRequest = 0; LastMessage = message;
        DropResolved?.Invoke(requestId, success, message);
    }
    private Vector3 GetDropReleasePoint(ItemData item)
    {
        var held = GetComponent<PlayerItemController>();
        var view = GetComponent<PlayerViewModeController>();
        var identity = GetComponent<NetworkIdentity>();
        bool local = !NetworkClient.active && !NetworkServer.active || identity != null && identity.isLocalPlayer;
        if (held != null && held.EquippedItemData == item)
        {
            if (local && view != null && view.CurrentViewMode == PlayerViewModeController.ViewMode.FirstPerson &&
                held.TryGetFirstPersonReleasePoint(out var first)) return first;
            if (held.TryGetThirdPersonReleasePoint(out var third)) return third;
        }
        var animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.isHuman)
        {
            var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand != null) return hand.position;
        }
        return transform.position + Vector3.up + transform.forward * .35f;
    }
    private static bool TryBounds(GameObject root, out Bounds bounds)
    {
        bounds = default; bool found = false;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            Encapsulate(renderer.transform, renderer.localBounds, ref bounds, ref found);
        }
        // Inactive staging colliders have empty world bounds; use their shape data instead.
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            Bounds local;
            if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
            else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2);
            else if (collider is CapsuleCollider capsule)
            {
                var size = Vector3.one * capsule.radius * 2; size[capsule.direction] = Mathf.Max(capsule.height, capsule.radius * 2);
                local = new Bounds(capsule.center, size);
            }
            else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
            else return false; // Unknown physics shape cannot be placed safely.
            Encapsulate(collider.transform, local, ref bounds, ref found);
        }
        return found && float.IsFinite(bounds.size.sqrMagnitude) && bounds.size.sqrMagnitude > .00001f;
    }
    private static void Encapsulate(Transform owner, Bounds local, ref Bounds bounds, ref bool found)
    {
        for (int i = 0; i < 8; i++)
        {
            var p = owner.TransformPoint(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; } else bounds.Encapsulate(p);
        }
    }
}
