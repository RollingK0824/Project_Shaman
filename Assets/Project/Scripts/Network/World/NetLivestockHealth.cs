using UnityEngine;
using UnityEngine.AI;
using Mirror;

// 가축 체력. 귀신의 가축 죽이기(NA_KillAnimal)가 Animal(귀신 대상)을 거쳐 여기에 데미지를 줌
// [서버] 체력이 0이 되면 배회를 멈추고 죽음 상태를 SyncVar로 보냄
// [서버, 클라] 죽으면 몸(렌더러·콜라이더)을 숨김. 그 자리에는 귀신 행동이 남긴 가축 사체 흔적이 대신 놓임

public class NetLivestockHealth : NetworkBehaviour, IDamageable
{
    [Tooltip("귀신 공격 한 번의 데미지는 NA_KillAnimal의 Damage (기본 1)")]
    [SerializeField, Min(0.1f)] private float _maxHealth = 1f;

    [SyncVar(hook = nameof(OnDeadChanged))] private bool _isDead;

    private float _health;

    public bool IsDead => _isDead;

    private void Awake()
    {
        _health = _maxHealth;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 늦게 들어온 클라: 이미 죽은 가축은 처음부터 숨김
        if (_isDead) HideBody();
    }

    public void ReceiveDamage(float amount, GameObject source = null)
    {
        if (!isServer || _isDead || amount <= 0f) return;

        _health -= amount;
        if (_health > 0f) return;

        _isDead = true;
        Debug.Log($"[NetLivestockHealth] {name} 죽음 (by {(source != null ? source.name : "?")})", this);

        if (TryGetComponent(out NetLivestock wander)) wander.enabled = false;
        if (TryGetComponent(out NavMeshAgent agent) && agent.enabled)
        {
            if (agent.isOnNavMesh) agent.ResetPath();
            agent.enabled = false;
        }

        HideBody();
    }

    private void OnDeadChanged(bool oldValue, bool newValue)
    {
        if (newValue) HideBody();
    }

    private void HideBody()
    {
        foreach (Renderer body in GetComponentsInChildren<Renderer>(true)) body.enabled = false;
        foreach (Collider body in GetComponentsInChildren<Collider>(true)) body.enabled = false;
    }
}
