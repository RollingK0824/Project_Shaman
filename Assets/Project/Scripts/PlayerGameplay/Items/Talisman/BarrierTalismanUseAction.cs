using System;
using System.Collections.Generic;
using Mirror;
using ProjectShaman.AI;
using ProjectShaman.AI.Ghost;
using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Use Actions/Barrier Talisman")]
public sealed class BarrierTalismanUseAction : ItemUseAction
{
    [SerializeField, Min(.1f)] private float _attachDistance = 3f;
    [SerializeField] private LayerMask _surfaceMask = (1 << 0) | (1 << 10); // Default, Road
    [SerializeField, Min(0f)] private float _surfaceOffset = .004f;
    [SerializeField, Range(-1f, 1f)] private float _minimumNormalY = -.2f;
    [SerializeField] private BarrierWard _barrierPrefab;
    private readonly HashSet<GameObject> _placing = new();

    // Presentation may call this without authority. The judge MUST repeat the raycast.
    public bool TryGetPlacement(in ItemUseContext context, out RaycastHit surface, out Quaternion rotation)
    {
        surface = default; rotation = Quaternion.identity;
        if (context.Owner == null || !Finite(context.AimOrigin) || !Finite(context.AimDirection) ||
            context.AimDirection.sqrMagnitude < .9f || !ValidSettings() ||
            Vector3.Distance(context.AimOrigin, context.Owner.transform.position) > 3f) return false;
        // Query all layers first: an excluded actor must occlude the wall behind it.
        var hits = Physics.RaycastAll(context.AimOrigin, context.AimDirection, _attachDistance,
            ~0, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(context.Owner.transform)) continue;
            if (!IsSurfaceAllowed(hit.collider) || hit.normal.y < _minimumNormalY) return false;
            surface = hit;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, hit.normal);
            if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(context.AimDirection, hit.normal);
            if (up.sqrMagnitude < .01f) up = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
            rotation = Quaternion.LookRotation(hit.normal, up.normalized);
            return true;
        }
        return false;
    }

    public bool IsSurfaceAllowed(Collider surface) => surface != null && !surface.isTrigger &&
        surface.enabled && surface.gameObject.activeInHierarchy && (_surfaceMask.value & (1 << surface.gameObject.layer)) != 0 &&
        surface.GetComponentInParent<PlayerHealth>() == null && surface.GetComponentInParent<PlayerController>() == null &&
        surface.GetComponentInParent<CharacterController>() == null && surface.GetComponentInParent<AI_Core>() == null &&
        surface.GetComponentInParent<AI_GhostEntity>() == null && surface.GetComponentInParent<WorldItem>() == null &&
        surface.GetComponentInParent<ItemBase>() == null && surface.GetComponentInParent<BarrierWard>() == null &&
        surface.GetComponentInParent<IItemControlReceiver>() == null && surface.GetComponentInParent<IDamageable>() == null;

    public override bool Execute(in ItemUseContext context)
    {
        // Persistent online objects need a server spawn/despawn adapter. Fail closed until connected.
        if (NetworkClient.active || NetworkServer.active || context.Owner == null) return false;
        var owner = context.Owner; var data = context.Data;
        var inventory = owner.GetComponent<PlayerInventory>();
        return TryPlaceAuthoritative(context, () => IsEquipped(owner, data),
            () => inventory != null && inventory.TryExchangePersonalItems(
                new Dictionary<ItemData, int> { [data] = 1 }, null, 0, null, null),
            ward => true, ward => Destroy(ward.gameObject), out _);
    }

    // Server adapter: validate/canonicalize aim + cooldown BEFORE calling. Call ReportUse once
    // ONLY on true. registerSpawn must synchronously register a disarmed ward; tryConsumeOne
    // must atomically spend exactly one and return success. Both callbacks must not throw.
    // despawn is also called on rollback. No client-supplied hit point/normal or effect values.
    public bool TryPlaceAuthoritative(in ItemUseContext context, Func<bool> isEquipped,
        Func<bool> tryConsumeOne, Func<BarrierWard, bool> registerSpawn, Action<BarrierWard> despawn,
        out BarrierWard barrier)
    {
        barrier = null;
        if (!ItemAttackAuthority.CanResolve(context.Owner) || !(context.Data is TalismanItemData data) ||
            data.TalismanType != TalismanType.Barrier || data.UseAction != this ||
            _barrierPrefab == null || !_barrierPrefab.IsConfigurationValid || isEquipped == null ||
            tryConsumeOne == null || registerSpawn == null || despawn == null ||
            !_placing.Add(context.Owner)) return false;
        BarrierWard created = null;
        bool committed = false;
        try
        {
            var inventory = context.Owner.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.HasItem(data, 1) || !isEquipped() ||
                !TryGetPlacement(context, out var hit, out var rotation)) return false;
            created = Instantiate(_barrierPrefab, hit.point + hit.normal * _surfaceOffset, rotation);
            created.Prepare(context.Owner, hit.collider, despawn);
            if (!registerSpawn(created) || created == null || !created.gameObject.activeInHierarchy ||
                !ItemAttackAuthority.CanResolve(context.Owner) || !isEquipped() || !tryConsumeOne()) return false;
            // Inventory observers may destroy the held item. This world object is independent.
            committed = true;
            created.Arm();
            barrier = created;
            return true;
        }
        catch (Exception e) { Debug.LogException(e, this); return false; }
        finally
        {
            _placing.Remove(context.Owner);
            if (!committed && created != null) created.Expire();
        }
    }

    public static bool IsEquipped(GameObject owner, ItemData data)
    {
        if (!PlayerActionGuard.CanAct(owner)) return false;
        var items = owner.GetComponent<PlayerItemController>();
        var inventory = owner.GetComponent<PlayerInventory>();
        return items != null && items.CanUseItems && items.EquippedItemData == data && inventory != null &&
            inventory.GetQuickSlotItem(items.SelectedSlotIndex) == data;
    }
    private bool ValidSettings() => float.IsFinite(_attachDistance) && _attachDistance > 0f &&
        float.IsFinite(_surfaceOffset) && _surfaceOffset >= 0f && float.IsFinite(_minimumNormalY);
    private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
}
