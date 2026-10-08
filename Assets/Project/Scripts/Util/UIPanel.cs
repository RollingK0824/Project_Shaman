using UnityEngine;

public abstract class UIPanel : MonoBehaviour
{
    [SerializeField] bool _unlocksCursor = true;
    public bool UnlocksCursor => _unlocksCursor;
}
