using UnityEngine;

// Presentation assets only; gameplay radii, effects and authority live in the existing UseActions.
public sealed class ItemVfxLibrary : ScriptableObject
{
    public GameObject BellPulse, GongPulse, BarrierActivate, TalismanActivate, BeanTrail, BeanImpact;
    public static ItemVfxLibrary Default => Resources.Load<ItemVfxLibrary>("ItemVfxLibrary");
}
