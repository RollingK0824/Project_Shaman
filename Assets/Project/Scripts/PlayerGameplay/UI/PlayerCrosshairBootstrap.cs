using UnityEngine;

public class PlayerCrosshairBootstrap : MonoBehaviour
{
    [SerializeField] private PlayerCrosshair _prefab;
    private PlayerCrosshair _instance;
    private PlayerInputReader _input;
    private void Awake() => _input = GetComponent<PlayerInputReader>();
    private void LateUpdate()
    {
        bool show = PlayerLocalPresentation.IsLocal(gameObject) && PlayerActionGuard.CanAct(gameObject) &&
                    _input != null && _input.isActiveAndEnabled && !_input.GameplayInputBlocked;
        if (show && _instance == null && _prefab != null) _instance = Instantiate(_prefab);
        if (_instance != null && _instance.gameObject.activeSelf != show) _instance.gameObject.SetActive(show);
    }
    private void OnDisable() { if (_instance != null) Destroy(_instance.gameObject); }
}
