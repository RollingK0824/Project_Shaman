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
        PlayerEvents.DeathConfirmed += HandleDeathConfirmed;
        PlayerRoster.Register(this);
    }

    public override void OnStopServer()
    {
        PlayerEvents.DeathConfirmed -= HandleDeathConfirmed;
        PlayerRoster.Unregister(this);
        base.OnStopServer();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        if (_isDead)
        {
            _health.ApplyDeath();
        }
    }

    // [서버] 판정기가 확정한 사망을 공개 상태로 전파
    private void HandleDeathConfirmed(PlayerHealth target, GameObject source)
    {
        if (target != _health)
        {
            return;
        }
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        PlayerRoster.NotifyChanged();
    }

    // [모든 클라, 호스트] 서버가 확정한 사망을 이 복사본에 적용
    private void OnIsDeadChanged(bool oldValue, bool newValue)
    {
        if (newValue)
        {
            _health.ApplyDeath();
        }

        DeadChanged?.Invoke(newValue);
    }
}
