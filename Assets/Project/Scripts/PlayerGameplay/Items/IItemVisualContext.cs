using UnityEngine;

// Optional visual-only binding, shared by future item types. No gameplay initialization.
public interface IItemVisualContext
{
    void BindItemVisual(ItemData item, GameObject owner);
}
