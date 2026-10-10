using UnityEngine;
using Mirror;

[RequireComponent(typeof(NetworkIdentity), typeof(PlayerHealth))]
public class NetPlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))] private float _currentHealth;


    private PlayerHealth _health;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _health.SetAuthorityCheck(() => !NetworkClient.active || NetworkServer.active);
    }

    // 에디터에서 컴포넌트를 추가할 때 HP가 다른 플레이어에게 전송되지 않도록 기본값 지정
    private void Reset()
    {
        syncMode = SyncMode.Owner;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _currentHealth = _health.CurrentHealth;
        PlayerEvents.DamageRequested += HandleDamageRequested;
        _health.HealthChanged += PublishHealth;
        _health.Died += PublishDeath;
    }

    public override void OnStopServer()
    {
        PlayerEvents.DamageRequested -= HandleDamageRequested;
        _health.HealthChanged -= PublishHealth;
        _health.Died -= PublishDeath;
        base.OnStopServer();
    }

    // 
    private void HandleDamageRequested(IDamageable target, float amount, GameObject source)
    {
        if (!ReferenceEquals(target, _health))
        {
            return;
        }

        if (_health.IsDead)
        {
            return;
        }

        if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0f)
        {
            return;
        }

        GameManager gm = GameManager.Instance;

        if (gm == null || gm.CurrentGameState != GameState.Ongoing)
        {
            return;
        }


        float prevHealth = _currentHealth;
        float nextHealth = Mathf.Max(prevHealth - amount, 0f);

        _health.ApplyHealth(nextHealth, source);
        _currentHealth = nextHealth;

        string sourceName = source != null ? source.name : "Unknown";
        Debug.Log($"[NetPlayerHealth] 피해 판정: {name} Hp {prevHealth : 0.##} -> {nextHealth : 0.##} / Source = {sourceName}", this);

        if (nextHealth <= 0f)
        {
            _health.ApplyDeath(source);

        }
    }

    private void PublishHealth(float current, float maximum) => _currentHealth = current;
    private void PublishDeath(GameObject source) => _currentHealth = 0f;

    private void OnHealthChanged(float oldValue, float newValue)
    {
        _health.ApplyHealth(newValue);
    }
}
