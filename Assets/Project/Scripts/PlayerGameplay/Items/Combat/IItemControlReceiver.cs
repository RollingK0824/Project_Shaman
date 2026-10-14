using System;
using UnityEngine;

public enum ItemControlKind { Slow, Stagger, SensoryInterference }
[Flags]
public enum ItemSense { None = 0, Vision = 1, Hearing = 2 }

[Serializable]
public struct ItemControlEffect
{
    public ItemControlKind Kind;
    [Min(0f)] public float Duration;
    [Range(0.01f, 1f)] public float SpeedMultiplier;
    public ItemSense Senses;
    public bool IsValid => !float.IsNaN(Duration) && !float.IsInfinity(Duration) && Duration > 0f &&
        (Kind == ItemControlKind.Stagger ||
         (Kind == ItemControlKind.Slow && !float.IsNaN(SpeedMultiplier) && SpeedMultiplier > 0f && SpeedMultiplier <= 1f) ||
         (Kind == ItemControlKind.SensoryInterference && Senses != ItemSense.None &&
          (Senses & ~(ItemSense.Vision | ItemSense.Hearing)) == 0));
}

// The receiver owns immunity, stacking, timers and restoration. This contract never changes HP.
public interface IItemControlReceiver
{
    bool TryApplyItemControl(ItemControlEffect effect, GameObject source);
}
