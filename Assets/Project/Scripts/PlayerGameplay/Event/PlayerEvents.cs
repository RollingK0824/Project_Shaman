using System;
using UnityEngine;

public static class PlayerEvents
{
    public static event Action<IDamageable, float, GameObject> DamageRequested;

    public static event Action<PlayerHealth, GameObject> DeathConfiremed;

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
        DeathConfiremed?.Invoke(target, source);
    }

    private static void ResetOnPlay()
    {
        DamageRequested = null;
        DeathConfiremed = null;
    }
}
