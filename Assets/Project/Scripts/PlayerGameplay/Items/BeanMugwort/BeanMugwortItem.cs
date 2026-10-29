using UnityEngine;

public class BeanMugwortItem : ItemBase
{
    public override void OnUseStarted() => BeginTimedUse(Release);
    private void Release(ItemUseContext context)
    {
        if (Data.UseAction is BeanMugwortUseAction action)
        {
            context = action.WithLocalReleaseOrigin(context);
            action.SpawnVisualProjectile(context);
        }
        PlayerEvents.RaiseItemUseRequested(context);
    }
}
