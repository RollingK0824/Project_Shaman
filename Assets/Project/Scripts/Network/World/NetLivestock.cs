using UnityEngine;
using UnityEngine.AI;
using Mirror;

// 가축 배회. 배치한 위치를 중심으로 반경 안에서 걷기 / 서 있기 / 풀 먹기를 반복, 밤에는 제자리에 멈춤
// [서버] NavMeshAgent로 이동하고 상태를 정함. 위치는 NetworkTransform이 클라로 보냄
// [클라] Agent를 끄고, 동기화된 상태를 보고 애니메이션만 바꿈

[RequireComponent(typeof(NavMeshAgent))]
public class NetLivestock : NetworkBehaviour
{
    private enum LivestockState : byte { Idle, Walk, Eat}

    [Header("배회")]
    [Tooltip("배치한 위치에서 이 거리 안에서만 돌아다님. 울타리 안쪽 크기보다 작게")]
    [SerializeField, Min(0.5f)] private float _wanderRadius = 4f;
    [SerializeField, Min(0.1f)] private float _walkSpeed = 0.8f;
    [SerializeField] private Vector2 _idleTime = new Vector2(3f, 8f);
    [SerializeField] private Vector2 _eatTime = new Vector2(5f, 12f);
    [SerializeField, Range(0f, 1f)] private float _eatChance = 0.4f;
    [Tooltip("길이 막혀 도착을 못하면 이 시간 뒤 포기")]
    [SerializeField, Min(1f)] private float _walkTimeout = 12f;


    [Header("애니메이션 (Animator 상태 이름)")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _idleState = "idle";
    [SerializeField] private string _walkState = "walk";
    [SerializeField] private string _eatState = "eat";
    [SerializeField, Min(0f)] private float _crossFade = 0.25f;

    [SyncVar] private LivestockState _state;

    private NavMeshAgent _agent;
    private Vector3 _home;

    // [서버] 다음 행동을 고를 시각 (걷는 중이면 포기 시각)
    private float _nextDecision;

    // [로컬] 지금 재생 중인 애니메이션 (바뀔 때만 Cross Fade)
    private LivestockState _playing = (LivestockState)255;

    private void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.enabled = false; // 서버에서만 켬
        if (_animator == null) _animator = GetComponentInChildren<Animator>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        _home = transform.position;
        _agent.enabled = true;
        _agent.speed = _walkSpeed;

        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[NetLivestock] {name} 위치에 NavMesh가 없음 -> 제자리에만 있음", this);
        }

        // 모든 가축이 동시에 움직이지 않도록 처음 대기 시간을 섞음
        Rest();
    }


    [ServerCallback]

    private void Update()
    {
        if (!_agent.enabled || !_agent.isOnNavMesh) return;

        // 밤: 그 자리에 서 있음
        TimeManager time = TimeManager.Existing;
        if (time != null && time.CurrentTimePhase == TimeOfDay.Night)
        {
            if (_state != LivestockState.Idle)
            {
                _agent.ResetPath();
                _state = LivestockState.Idle;
            }
            _nextDecision = Time.time + Random.Range(_idleTime.x, _idleTime.y);
            return;
        }

        if (_state == LivestockState.Walk)
        {
            bool arrived = !_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance;
            if (arrived || Time.time >= _nextDecision)
            {
                _agent.ResetPath();
                Rest();
            }
            return;
        }

        if (Time.time >= _nextDecision) WalkToRandomPoint();

    }

    // [서버] 집 반경 안의 NavMesh 위 한 점으로 걷기
    private void WalkToRandomPoint()
    {
        Vector2 offset = Random.insideUnitCircle * _wanderRadius;
        Vector3 candidate = _home + new Vector3(offset.x, 0f, offset.y);

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, _agent.areaMask) && _agent.SetDestination(hit.position))
        {
            _state = LivestockState.Walk;
            _nextDecision = Time.time + _walkTimeout;
        }
        else
        {
            _nextDecision = Time.time + 1f; // 갈 곳을 못 찾으면 잠시 뒤 다시
        }
    }

    // [서버] 도착 후 서 있거나 풀 먹기
    private void Rest()
    {
        bool eat = Random.value < _eatChance;
        _state = eat ? LivestockState.Eat : LivestockState.Idle;

        Vector2 range = eat ? _eatTime : _idleTime;
        _nextDecision = Time.time + Random.Range(range.x, range.y);
    }

    // [서버, 클라] 상태가 바뀌었을 때만 애니메이션 전환
    private void LateUpdate()
    {
        if (_animator == null || _playing == _state) return;
        _playing = _state;

        string stateName = _state switch
        {
            LivestockState.Walk => _walkState,
            LivestockState.Eat => _eatState,
            _ => _idleState,
        };

        _animator.CrossFadeInFixedTime(stateName, _crossFade);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Application.isPlaying && isServer ? _home : transform.position, _wanderRadius);
    }
}
