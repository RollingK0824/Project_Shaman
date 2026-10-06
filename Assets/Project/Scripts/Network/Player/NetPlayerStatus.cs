using System;
using Mirror;
using UnityEngine;

// 서버의 PlayerHealth가 확정한 공개 상태(이름, 사망 여부, 스트레스 단계)를 모든 클라이언트에 전달합니다.
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerHealth))]
public class NetPlayerStatus : NetworkBehaviour
{
    [SyncVar] private string _playerName;
    [SyncVar(hook = nameof(OnIsDeadChanged))] private bool _isDead;

    // [서버 -> 모든 클라] 스트레스 단계. 떨림,숨소리등 같은 다른 사람에게 보이는 증상용
    [SyncVar(hook = nameof(OnStressLevelChanged))] private PlayerStressController.StressLevel _stressLevel;

    private PlayerHealth _health;
    private PlayerStressController _stress;

    public string PlayerName => _playerName;
    public bool IsDead => _isDead;
    public PlayerStressController.StressLevel StressLevel => _stressLevel;

    // 클라이언트의 사망 적용 및 관전 전환은 후속 작업에서 연결합니다.
    public event Action<bool> DeadChanged;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _stress = GetComponent<PlayerStressController>();
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
        if (_stress != null) _stressLevel = _stress.CurrentLevel;

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

        if (!isServer && !isOwned && _stress != null)
        {
            _stress.ApplyStressLevel(_stressLevel);
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

    // [서버] NetPlayerStress가 판정 후 호출. 값이 같으면 Mirror가 전송x
    [Server]
    public void ServerSetStressLevel(PlayerStressController.StressLevel level)
    {
        _stressLevel = level;
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

    // [다른 클라] 서버가 확정한 스트레스 단계를 복사본에 적용 / 연출은 StressLevelChanged를 구독
    private void OnStressLevelChanged(PlayerStressController.StressLevel oldValue, PlayerStressController.StressLevel newValue)
    {
        if (isServer || isOwned) return;
        if (_stress == null) return;

        _stress.ApplyStressLevel(newValue);
    }

}
