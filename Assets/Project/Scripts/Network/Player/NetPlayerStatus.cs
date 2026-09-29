using System;
using Mirror;
using UnityEngine;

// 서버의 PlayerHealth가 확정한 공개 상태만 모든 클라이언트에 전달합니다.
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerHealth))]
public class NetPlayerStatus : NetworkBehaviour
{
    [SyncVar] private string _playerName;
    [SyncVar(hook = nameof(OnIsDeadChanged))] private bool _isDead;

    private PlayerHealth _health;

    public string PlayerName => _playerName;
    public bool IsDead => _isDead;

    // 클라이언트의 사망 적용 및 관전 전환은 후속 작업에서 연결합니다.
    public event Action<bool> DeadChanged;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
    }

    [Server]
    public void ServerInitialize(string playerName)
    {
        _playerName = playerName;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _isDead = _health.IsDead;
        _health.Died += HandleDiedOnServer;
        PlayerRoster.Register(this);
    }

    public override void OnStopServer()
    {
        _health.Died -= HandleDiedOnServer;
        PlayerRoster.Unregister(this);
        base.OnStopServer();
    }

    private void HandleDiedOnServer(GameObject source)
    {
        if (_isDead) return;

        _isDead = true;
        PlayerRoster.NotifyChanged();
    }

    private void OnIsDeadChanged(bool oldValue, bool newValue)
    {
        DeadChanged?.Invoke(newValue);
    }
}
