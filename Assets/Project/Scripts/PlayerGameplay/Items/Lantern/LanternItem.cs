using UnityEngine;

// Equip-only presentation. The existing item controller owns FP/TP renderer visibility.
// No use request, fuel, stress effect or additional network state.
[DefaultExecutionOrder(250)]
public sealed class LanternItem : ItemBase, IItemVisualContext
{
    [SerializeField] private Light _light;
    [SerializeField] private Renderer _visibleSurface;
    private GameObject _visualOwner;

    public void BindItemVisual(ItemData item, GameObject owner)
    {
        _visualOwner = owner;
        SetLit(false);
    }

    private void OnEnable() => SetLit(false);
    private void LateUpdate()
    {
        // Hidden first/third-person copies must never illuminate the scene together.
        SetLit(_visualOwner != null && PlayerActionGuard.CanAct(_visualOwner) &&
            _visibleSurface != null && _visibleSurface.enabled && _visibleSurface.gameObject.activeInHierarchy);
    }
    private void SetLit(bool lit) { if (_light != null) _light.enabled = lit; }
    public override void OnUseStarted() { } // Automatically lit while equipped; no click toggle.
    public override void OnUnequipped() => SetLit(false);
    private void OnDisable() => SetLit(false);
}
