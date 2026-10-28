using System;
using System.Collections;
using ProjectShaman.ItemHold;
using UnityEngine;

// One local windup -> one release -> recovery. Remote RPCs schedule presentation only.
[DisallowMultipleComponent]
public sealed class PlayerTimedItemUse : MonoBehaviour
{
    public bool IsBusy { get; private set; }
    ItemData _item;
    bool _released;
    int _generation;
    public bool HideHeldItem(ItemData data) => IsBusy && _released && data == _item && _item != null && _item.UseAction is BeanMugwortUseAction;
    public void BeginLocal(ItemUseContext context, Action<ItemUseContext> release)
    {
        if (IsBusy || !isActiveAndEnabled) return;
        var controller = GetComponent<PlayerItemController>();
        if (controller == null || !controller.CanStartTimedUse || controller.EquippedItemData != context.Data) return;
        StartUse(context, release, false);
        controller.PlayUseMotion(context.Data);
    }
    public void BeginRemote(ItemUseContext context, Action<ItemUseContext> release)
    {
        if (IsBusy || !isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject)) return;
        StartUse(context, release, true);
    }
    void StartUse(ItemUseContext context, Action<ItemUseContext> release, bool remote)
    {
        _item = context.Data; _released = false; IsBusy = true;
        StartCoroutine(Run(++_generation, context, release, remote));
    }
    IEnumerator Run(int generation, ItemUseContext context, Action<ItemUseContext> release, bool remote)
    {
        var controller = GetComponent<PlayerItemController>();
        int slot = controller != null ? controller.SelectedSlotIndex : -1;
        var held = controller != null ? controller.EquippedBehaviour : null;
        float started = Time.time;
        float releaseAt = started + ItemHoldDriver.TimedReleaseDelay(context.Data.ThirdPersonUseMotionId);
        float finishAt = started + ItemHoldDriver.TimedUseDuration(context.Data.ThirdPersonUseMotionId);
        try
        {
            while (Time.time < releaseAt)
            {
                if (!Valid(controller, context.Data, held, slot, remote)) yield break;
                yield return null;
            }
            if (!Valid(controller, context.Data, held, slot, remote)) yield break;
            var resolved = context;
            if (!remote)
            {
                var camera = GetComponentInChildren<Camera>();
                if (camera == null) yield break;
                resolved = new ItemUseContext(gameObject, context.Data, camera.transform.position, camera.transform.forward);
                GetComponent<PlayerItemFeedback>()?.Play(context.Data);
            }
            _released = true;
            release(resolved);
            while (Time.time < finishAt && PlayerActionGuard.CanAct(gameObject)) yield return null;
        }
        finally { if (generation == _generation) { IsBusy = false; _item = null; _released = false; } }
    }
    bool Valid(PlayerItemController controller, ItemData item, ItemBase held, int slot, bool remote)
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return false;
        if (remote) return true;
        var input = GetComponent<PlayerInputReader>();
        return held != null && controller != null && controller.EquippedBehaviour == held && controller.CanUseItems && controller.EquippedItemData == item && controller.SelectedSlotIndex == slot &&
            (input == null || !input.GameplayInputBlocked) && GetComponent<PlayerInventory>().Contains(item);
    }
    void OnDisable() { ++_generation; StopAllCoroutines(); IsBusy = false; _item = null; _released = false; }
}
