using System;
using UnityEngine;

public static class PlayerEvents
{
    // 피해원이 대상에게 피해를 주려고 할 때 (NetPlayerHealth 또는 오프라인쪽에서 구독)
    public static event Action<IDamageable, float, GameObject> DamageRequested;

    // 판정기가 플레이어 사망을 확정했을 때
    public static event Action<PlayerHealth, GameObject> DeathConfirmed;

    // 플레이어가 월드 아이템을 주으려고 할 때 (네트워크에서 본인의 NetPlayerInventory가 서버로 전달)
    public static event Action<GameObject, WorldItem> PickupRequested;

    public static void RaiseDamageRequested(IDamageable target, float amount, GameObject source)
    {
        if (target == null)
        {
            return;
        }

        DamageRequested?.Invoke(target, amount, source);
    }

    public static void RaiseDeathConfirmed(PlayerHealth target, GameObject source)
    {
        if (target == null)
        {
            return;
        }

        string sourceName = source != null ? source.name : "Unknown";
        Debug.Log($"[PlayerEvents] 사망 확정 : {target.name} / Source = {sourceName}", target);
        DeathConfirmed?.Invoke(target, source);
    }

    public static void RaisePickupRequested(GameObject interactor, WorldItem item)
    {
        if (interactor == null || item == null)
        {
            return;
        }

        PickupRequested?.Invoke(interactor, item);
    }


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        DamageRequested = null;
        DeathConfirmed = null;
        PickupRequested = null;
    }
}
