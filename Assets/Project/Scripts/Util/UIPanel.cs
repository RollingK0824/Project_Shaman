using UnityEngine;

public abstract class UIPanel
{
    [SerializeField] bool _unlocksCursor = true;
    public bool UnlocksCursor => _unlocksCursor;
}
