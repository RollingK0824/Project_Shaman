using UnityEngine;

// Camera correction runs at 100 and held-item fitting at 200.
[DefaultExecutionOrder(300)]
public class BeanMugwortItem : ItemBase
{
    private bool _aiming;
    private BeanMugwortTrajectory _trajectory;
    public bool IsAiming => _aiming;

    private bool CanAim()
    {
        if (Owner == null || !(Data?.UseAction is BeanMugwortUseAction) || !PlayerLocalPresentation.IsLocal(Owner)) return false;
        var items = Owner.GetComponent<PlayerItemController>();
        var input = Owner.GetComponent<PlayerInputReader>();
        var timed = Owner.GetComponent<PlayerTimedItemUse>();
        var view = Owner.GetComponent<PlayerViewModeController>();
        return PlayerActionGuard.CanAct(Owner) && items != null && items.EquippedBehaviour == this &&
            items.CanStartTimedUse && items.RemainingUseCooldown(Data) <= 0f &&
            (input == null || input.CanReadItemInput) && (timed == null || !timed.IsBusy) &&
            (view == null || !view.IsBusy) && Owner.GetComponent<PlayerInventory>().Contains(Data);
    }

    public override void OnUseStarted()
    {
        if (_aiming || !CanAim()) return;
        _aiming = true;
        if (_trajectory == null) _trajectory = new GameObject("BeanMugwort aim trajectory").AddComponent<BeanMugwortTrajectory>();
        UpdateTrajectory();
    }

    private void LateUpdate()
    {
        if (!_aiming) return;
        if (!CanAim()) { OnUseCanceled(); return; }
        UpdateTrajectory();
    }

    private void UpdateTrajectory()
    {
        var camera = Owner.GetComponentInChildren<Camera>();
        if (camera == null) { OnUseCanceled(); return; }
        var action = (BeanMugwortUseAction)Data.UseAction;
        var context = action.WithLocalReleaseOrigin(new ItemUseContext(Owner, Data, camera.transform.position, camera.transform.forward));
        _trajectory.Show(action, context);
    }

    public override void OnUseReleased()
    {
        bool release = _aiming && CanAim();
        OnUseCanceled();
        if (!release || !TryBuildUseRequest(out var context, false)) return;
        context = ((BeanMugwortUseAction)Data.UseAction).WithLocalReleaseOrigin(context);
        var timed = Owner.GetComponent<PlayerTimedItemUse>() ?? Owner.AddComponent<PlayerTimedItemUse>();
        // Freeze the displayed aim and launch on mouse-up; recovery still prevents repeated throws.
        timed.BeginLocal(context, Release, true, true);
    }
    private void Release(ItemUseContext context)
    {
        ((BeanMugwortUseAction)context.Data.UseAction).SpawnVisualProjectile(context);
        PlayerEvents.RaiseItemUseRequested(context);
    }

    public override void OnUseCanceled()
    {
        _aiming = false;
        if (_trajectory != null) _trajectory.Hide();
    }
    public override void OnUnequipped() => OnUseCanceled();
    private void OnDisable() => OnUseCanceled();
    private void OnApplicationFocus(bool focused) { if (!focused) OnUseCanceled(); }
    private void OnDestroy() { if (_trajectory != null) Destroy(_trajectory.gameObject); }
}
