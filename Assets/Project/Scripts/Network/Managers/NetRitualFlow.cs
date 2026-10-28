using UnityEngine;
using Mirror;
using ProjectShaman.AI.Defense;


// [서버 전용] 지목 판정 -> 깃발 의식 -> 디펜스 -> 퇴마 보고를 연결
// 클라에 보여줄 상태 (의식 가능 여부, 남은 시간, 깃발 HP)는 다음 단계에서 따로 동기화
[RequireComponent(typeof(NetGM))]
public class NetRitualFlow : NetworkBehaviour
{
    [SerializeField] private DefenseVoteResolver _voteResolver;
    [SerializeField] private DefenseManager _defense;
    [SerializeField] private ExorcismFlagInteractable _flag;

    [Header("의식 시간")]
    [Tooltip("밤 시작 후 이 시간 안에 깃발을 켜야 함. 못 켜면 오늘 밤 퇴마 실패")]
    [SerializeField, Min(1f)] private float _activateWindow = 30f;

    [Header("테스트")]
    [Tooltip("켜면 지목이 틀려도 의식 디펜스가 시작됨")]
    [SerializeField] private bool _debugForceRitual;

    private TimeManager _timeM;
    private NetDefenseState _state;

    // [서버] 오늘 밤 의식이 열렸는지 (지목 정답일 때만 true) 판정 내용은 클라로 보내지 않음
    private bool _hasRitual;
    // [서버] 깃발 활성화 마감 시각 (NetworkTime 기준)
    private double _activateDeadline;

    private bool _serverSubscribed;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _state = GetComponent<NetDefenseState>();
        _timeM = TimeManager.Instance;

        if (_voteResolver == null || _defense == null || _flag == null || _timeM == null || _state == null)
        {
            Debug.LogError("[NetRitualFlow] 참조 누락 (Resolver, DefenseManager, 깃발, TimeManager)", this);
            return;
        }

        _voteResolver.OnResolved += HandleResolved;
        _flag.Activated += HandleFlagActivated;
        _defense.DefenseCompleted += HandleDefenseCompleted;
        _timeM.OnDayStart += HandleDayStart;
        _defense.DefenseCountdownStarted += HandleCountdownStarted;
        _defense.DefenseStarted += HandleDefenseStarted;
        _serverSubscribed = true;
    }

    public override void OnStopServer()
    {
        if (_serverSubscribed)
        {
            _voteResolver.OnResolved -= HandleResolved;
            _flag.Activated -= HandleFlagActivated;
            _defense.DefenseCompleted -= HandleDefenseCompleted;
            _timeM.OnDayStart -= HandleDayStart;
            _defense.DefenseCountdownStarted -= HandleCountdownStarted;
            _defense.DefenseStarted -= HandleDefenseStarted;
        }

        _serverSubscribed = false;
        base.OnStopServer();
    }

    // [서버] 밤 시작 때 지목 판정.. 정답일 때만 깃발 의식 활성화
    private void HandleResolved(DefenseVerdict verdict)
    {
        _hasRitual = verdict.IsCorrect || _debugForceRitual;

        if (_hasRitual)
        {
            _flag.SetRitualActive(true);
        }
        _activateDeadline = NetworkTime.time + _activateWindow;
        _state.ServerSetPhase(_hasRitual ? RitualPhase.WaitingFlag : RitualPhase.NoRitual,
            _hasRitual ? _activateDeadline : NightEndTime());

        Debug.Log($"[NetRitualFlow] 지목 판정 -> 의식 {(_hasRitual ? "열림" : "없음")}", this);
    }

    // [서버] 깃발 활성화 확정(NetExorcismFlag가 거리,생존 검사 후 TryActivate) -> 디펜스 시작
    private void HandleFlagActivated(ExorcismFlagInteractable flag, GameObject activator)
    {
        if (!_hasRitual)
        {
            return;
        }

        _defense.StartDefense();
        Debug.Log($"[NetRitualFlow] 깃발 활성화 -> 디펜스 준비 ({activator.name})", this);
    }

    private void HandleCountdownStarted(float delay)
    {
        _state.ServerSetPhase(RitualPhase.Countdown, NetworkTime.time + delay);
    }

    private void HandleDefenseStarted()
    {
        _state.ServerSetPhase(RitualPhase.Active, NetworkTime.time + _defense.DefenseDuration);
    }

    private double NightEndTime()
    {
        return _timeM.CycleStartTime + _timeM.DayCount * TimeManager.CYCLE_DURATION;
    }

    // [서버] 깃발 활성화 마감. 시간 안에 못 켜면 오늘 밤 의식은 닫히고 퇴마 실패
    [ServerCallback]
    private void Update()
    {
        if (!_serverSubscribed || !_hasRitual || _state.Phase != RitualPhase.WaitingFlag) return;
        if (NetworkTime.time < _activateDeadline) return;

        _hasRitual = false;
        _flag.SetRitualActive(false); 
        _state.ServerSetPhase(RitualPhase.FlagMissed);

        Debug.Log("[NetRitualFlow] 깃발 활성화 마감 -> 퇴마 실패", this);
    }


    // [서버] 디펜스 버팀 = true, 깃발 HP 0 = false
    private void HandleDefenseCompleted(bool won)
    {
        if (!_hasRitual)
        {
            return;
        }

        _hasRitual = false;

        _state.ServerSetPhase(won ? RitualPhase.Succeeded : RitualPhase.Failed);

        Debug.Log($"[NetRitualFlow] 디펜스 종료 -> 디펜스 {(won ? "성공" : "실패")}", this);
    }

    // [서버] 아침이 되면 깃발 비활성화
    private void HandleDayStart()
    {
        _flag.ResetFlag();
        _flag.SetRitualActive(false);

        // DefenseManager도 OnDayStart로 성공 판정을 하는데 둘 중 누가 먼저 호출될지는 정해져 있지 않음
        if (_defense.State != DefenseState.Active)
        {
            _hasRitual = false;

            RitualPhase phase = _state.Phase;
            if (phase == RitualPhase.WaitingFlag || phase == RitualPhase.Countdown)
            {
                _state.ServerSetPhase(RitualPhase.Failed);
            }
            else if (phase == RitualPhase.NoRitual)
            {
                _state.ServerSetPhase(RitualPhase.None);
            }
        }
    }
}
