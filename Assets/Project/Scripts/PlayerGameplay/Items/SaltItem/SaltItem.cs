using UnityEngine;

public class SaltItem : ItemBase
{
    private ParticleSystem _legacyEmitter;
    private void Awake()
    {
        _legacyEmitter = GetComponentInChildren<ParticleSystem>(true);
        if (_legacyEmitter == null) return;
        _legacyEmitter.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _legacyEmitter.main; main.playOnAwake = false;
        _legacyEmitter.GetComponent<ParticleSystemRenderer>().enabled = false;
    }
    public override void OnUseStarted() => BeginTimedUse(Release);
    private void Release(ItemUseContext context)
    {
        var action = Data.UseAction as SaltUseAction;
        var request = context;
        if (action != null && !action.TryBuildRequest(context, out request)) return;
        float reach = action != null ? action.VisualReach : 3f;
        float width = action != null ? action.VisualHalfWidth : .2f;
        var view = Owner.GetComponent<PlayerViewModeController>();
        if (view != null && view.CurrentViewMode == PlayerViewModeController.ViewMode.ThirdPerson)
            Owner.GetComponent<PlayerItemController>()?.PlayThirdPersonSaltSpray(context.AimDirection, context.AimOrigin, reach, width);
        else
            SaltVfxBurst.Spray(Owner, _legacyEmitter != null ? _legacyEmitter.transform.position : transform.position,
                new Ray(context.AimOrigin, context.AimDirection), reach, width);
        PlayerEvents.RaiseItemUseRequested(request);
    }
}
