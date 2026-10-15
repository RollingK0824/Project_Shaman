using Mirror;
using UnityEngine;

public class PlayerStressHUDBootstrap : MonoBehaviour
{
    [SerializeField] private PlayerStressHUD _hudPrefab;
    [SerializeField] private PlayerStressController _stress;
    [SerializeField] private PlayerHealth _health;
    private NetworkIdentity _identity;
    private PlayerStressHUD _instance;
    private bool _visible;
    private void Awake()
    {
        if (_stress == null) _stress = GetComponent<PlayerStressController>();
        if (_health == null) _health = GetComponent<PlayerHealth>();
        _identity = GetComponent<NetworkIdentity>();
    }
    // Only ownership state is polled; UI values remain event driven.
    private void LateUpdate()
    {
        bool local = (!NetworkClient.active && !NetworkServer.active) || (_identity != null && _identity.isLocalPlayer);
        if (!local || _stress == null)
        {
            if (_visible && _instance != null) _instance.gameObject.SetActive(false);
            _visible = false;
            return;
        }
        if (_instance == null && _hudPrefab != null)
        {
            _instance = Instantiate(_hudPrefab); // Independent UI root, never a Player child.
            _instance.name = "Local Stress HUD";
            _instance.Bind(_stress, _health);
        }
        if (!_visible && _instance != null) _instance.gameObject.SetActive(true);
        _visible = _instance != null;
    }
    private void OnDisable()
    {
        if (_instance != null) { _instance.gameObject.SetActive(false); Destroy(_instance.gameObject); }
        _instance = null;
        _visible = false;
    }
}
