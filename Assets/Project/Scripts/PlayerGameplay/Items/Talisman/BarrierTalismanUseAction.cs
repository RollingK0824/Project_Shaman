using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

[CreateAssetMenu(menuName = "Project Shaman/Use Actions/Barrier Talisman")]
public sealed class BarrierTalismanUseAction : ItemUseAction
{
    [SerializeField] private BarrierWard _barrierPrefab;
    private readonly HashSet<GameObject> _activating = new();

    public bool CanActivate(GameObject owner) => PlayerActionGuard.CanAct(owner) &&
        owner.GetComponent<PlayerStressController>() != null && !BarrierWard.IsProtecting(owner) &&
        _barrierPrefab != null && _barrierPrefab.IsConfigurationValid;

    public override bool Execute(in ItemUseContext context)
    {
        // Persistent online objects need a server spawn/despawn adapter. Fail closed until connected.
        if (NetworkClient.active || NetworkServer.active || context.Owner == null) return false;
        var owner = context.Owner; var data = context.Data;
        var inventory = owner.GetComponent<PlayerInventory>();
        return TryActivateAuthoritative(context, () => IsEquipped(owner, data),
            () => inventory != null && inventory.TryExchangePersonalItems(
                new Dictionary<ItemData, int> { [data] = 1 }, null, 0, null, null),
            ward => true, ward => Destroy(ward.gameObject), out _);
    }

    // Server adapter: validate ownership + cooldown BEFORE calling. Call ReportUse once
    // ONLY on true. registerSpawn must synchronously register a disarmed ward; tryConsumeOne
    // must atomically spend exactly one and return success. Both callbacks must not throw.
    // despawn is also called on rollback. No client-supplied duration or protection values.
    public bool TryActivateAuthoritative(in ItemUseContext context, Func<bool> isEquipped,
        Func<bool> tryConsumeOne, Func<BarrierWard, bool> registerSpawn, Action<BarrierWard> despawn,
        out BarrierWard barrier)
    {
        barrier = null;
        if (!ItemAttackAuthority.CanResolve(context.Owner) || !(context.Data is TalismanItemData data) ||
            data.TalismanType != TalismanType.Barrier || data.UseAction != this ||
            _barrierPrefab == null || !_barrierPrefab.IsConfigurationValid || isEquipped == null ||
            tryConsumeOne == null || registerSpawn == null || despawn == null ||
            !_activating.Add(context.Owner)) return false;
        BarrierWard created = null;
        bool committed = false;
        try
        {
            var inventory = context.Owner.GetComponent<PlayerInventory>();
            if (inventory == null || !inventory.HasItem(data, 1) || !isEquipped() || !CanActivate(context.Owner)) return false;
            created = Instantiate(_barrierPrefab, context.Owner.transform.position, Quaternion.identity);
            created.Prepare(context.Owner, despawn);
            if (!registerSpawn(created) || created == null || !created.gameObject.activeInHierarchy ||
                !ItemAttackAuthority.CanResolve(context.Owner) || !isEquipped() || !CanActivate(context.Owner) || !tryConsumeOne()) return false;
            // Inventory observers may destroy the held item. This world object is independent.
            committed = true;
            created.Arm();
            barrier = created;
            return true;
        }
        catch (Exception e) { Debug.LogException(e, this); return false; }
        finally
        {
            _activating.Remove(context.Owner);
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
}
