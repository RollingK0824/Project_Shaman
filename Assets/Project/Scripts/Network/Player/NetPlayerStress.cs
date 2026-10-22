using UnityEngine;
using Mirror;

// [서버] 플레이어 스트레스의 원본을 판정,보관
// 수치는 소유 클라에게만 동기화(Sync Mode = Owner)

// 다른 클라에게 보이는 단계(ex 떨림, 숨소리 등)는 NetPlayerStatus가 공개
// 귀신 AI 등 서버 로직은 서버의 PlayerStressController.CurrentStress를 읽으면 됨
[RequireComponent(typeof(NetworkIdentity), typeof(PlayerStressController))]
public class NetPlayerStress : NetworkBehaviour
{
    // [서버 -> 소유 클라] 확정 스트레스 수치
    [SyncVar(hook = nameof(OnStressChanged))] private float _currentStress;

    private PlayerStressController _stress;
    private PlayerHealth _health;
    private NetPlayerStatus _status;
    private PlayerStressDamage _stressDamage;

    [Header("시야 스트레스 중계 (원격 클라 → 서버)")]
    [SerializeField, Min(0f)] private float _maxSightStressPerRequest = 40f;
    [SerializeField, Min(0f)] private float _minSightRequestInterval = 0.5f;
    [SerializeField, Min(0f)] private float _maxSightSourceDistance = 25f;
    private double _serverNextSightTime;

    private void Awake()
    {
        _stress = GetComponent<PlayerStressController>();
        _stress.UseExternalAuthority = true;
        _health = GetComponent<PlayerHealth>();
        _status = GetComponent<NetPlayerStatus>();
        _stressDamage = GetComponent<PlayerStressDamage>();
    }

    // 에디터에서 컴포넌트를 추가할 때 수치가 다른 클라에게는 전송되지 않도록
    private void Reset()
    {
        syncMode = SyncMode.Owner;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        _currentStress = _stress.CurrentStress;
        _stress.StressChanged += ServerHandleStressChanged;
        PlayerEvents.StressRequested += HandleStressRequested;
        // [서버] 지속 스트레스 피해는 서버에서만, 게임 진행 중일 때만 계산
        if (_stressDamage != null) _stressDamage.SetServerAuthorityCheck(ServerCanApplyStressDamage);
    }

    public override void OnStopServer()
    {
        PlayerEvents.StressRequested -= HandleStressRequested;
        _stress.StressChanged -= ServerHandleStressChanged;
        if (_stressDamage != null) _stressDamage.SetServerAuthorityCheck(null);
        base.OnStopServer();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        // 호스트의 로컬 플레이어는 이미 서버라 HandleStressRequested가 바로 받음 → 원격 클라만 중계
        if (!isServer) PlayerEvents.StressRequested += ClientForwardSightStress;
    }

    public override void OnStopLocalPlayer()
    {
        PlayerEvents.StressRequested -= ClientForwardSightStress;
        base.OnStopLocalPlayer();
    }

    // [서버] 버스로 돌아온 스트레스 요청을 판정. 모든 플레이어의 요청이 오므로 내 것만 처리
    private void HandleStressRequested(PlayerStressController target, float delta, StressCause cause, GameObject source)
    {
        if (target != _stress || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        if (_health != null && _health.IsDead) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return;
        if (!NetGameStartGate.HasStarted) return;

        _stress.SetStress(_stress.CurrentStress + delta);
    }

    // [로컬 클라] 카메라 시야 판정은 로컬 클라만 가능 → 결과만 서버에 보고
    private void ClientForwardSightStress(PlayerStressController target, float delta, StressCause cause, GameObject source)
    {
        if (target != _stress || !IsSightCause(cause)) return;

        NetworkIdentity sourceIdentity = source != null ? source.GetComponentInParent<NetworkIdentity>() : null;
        CmdReportSightStress(delta, cause, sourceIdentity);
    }

    // [서버] 클라 보고를 그대로 믿지 않고 원인, 상한, 간격, 거리 검증
    [Command]
    private void CmdReportSightStress(float delta, StressCause cause, NetworkIdentity source)
    {
        if (!IsSightCause(cause) || float.IsNaN(delta) || float.IsInfinity(delta) || delta <= 0f) return;
        if (NetworkTime.time < _serverNextSightTime) return;
        if (source != null &&
            (source.transform.position - transform.position).sqrMagnitude > _maxSightSourceDistance * _maxSightSourceDistance) return;

        _serverNextSightTime = NetworkTime.time + _minSightRequestInterval;

        // 클라에서 부적 배율(ModifyDelta)이 이미 적용된 값 → ReceiveStress를 다시 타면 배율이 두 번 들어감
        HandleStressRequested(_stress, Mathf.Min(delta, _maxSightStressPerRequest), cause, source != null ? source.gameObject : null);
    }

    private static bool IsSightCause(StressCause cause) => cause == StressCause.GhostSight || cause == StressCause.AnomalySight;

    // [서버] HandleStressRequested와 같은 조건. 로딩 중이나 게임 종료 뒤에는 피해 누적 안 함
    private bool ServerCanApplyStressDamage()
    {
        if (!isServer) return false;

        GameManager gm = GameManager.Instance;
        return gm != null && gm.CurrentGameState == GameState.Ongoing && NetGameStartGate.HasStarted;
    }
    private void ServerHandleStressChanged(float value)
    {
        _currentStress = value;
        if (_status != null) _status.ServerSetStressLevel(_stress.CurrentLevel);
    }

    private void OnStressChanged(float oldValue, float newValue)
    {
        if (isServer) return;

        _stress.SetStress(newValue);
    }
}
