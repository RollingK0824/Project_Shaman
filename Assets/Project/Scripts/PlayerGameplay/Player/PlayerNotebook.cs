using System;
using UnityEngine;

// Intrinsic player capability. No inventory unit, hotkey, UI or opening animation.
[DisallowMultipleComponent]
public class PlayerNotebook : MonoBehaviour
{
    public bool HasNotebook => true;
    public event Action NotebookOpenRequested;
    public bool RequestOpen()
    {
        if (!isActiveAndEnabled || !PlayerActionGuard.CanAct(gameObject) || !PlayerLocalPresentation.IsLocal(gameObject)) return false;
        NotebookOpenRequested?.Invoke();
        return true;
    }
}
