using UnityEngine;
using System;
using Mirror;

public enum RitualPhase : byte
{
    None,           // 낮, 의식과 무관한 시간
    NoRitual,       // 밤: 지목이 빙의가 아님 -> 의식 x
    WaitingFlag,    // 밤: 의식 가능, 깃발 활성화 대기
    Countdown,      // 깃발 활성화, 잡귀가 오기 전 준비 시간
    Active,         // 디펜스 중
    Succeeded,      // 디펜스 성공(아침까지 버팀)
    Failed          // 깃발 파괴, 또는 깃발을 켜지 않고 아침
}

// [서버 ->모든 클라] 디펜스 진행 상태. UI는 이 컴포넌트만 본다.
public class NetDefenseState : NetworkBehaviour
{
    // 깃발의 IDefenseTarget(지금은 MockDefenseFlag), AI가 정식 컴포넌트로 바꿔도 인터페이스라 그대로 사용
    [SerializeField] private MonoBehaviour _flagTarget;

    // 순서 주의: 클라에서는 선언 순서대로 값이 들어오고 hook이 불림
    // _phase hook에서 남은 시간/HP를 읽기에 _phase를 맨 마지막에 둔다.

    [SyncVar] private double _phaseEndTime;
    [SyncVar] private float _flagHp;
    [SyncVar] private float _flagMaxHp;
    [SyncVar(hook = nameof(OnPhaseChanged))] private RitualPhase _phase;

    private IDefenseTarget _target;
    public static NetDefenseState Instance { get; private set; }
    public RitualPhase Phase => _phase;
    public float FlagHP => _flagHp;
    public float FlagMaxHP => _flagMaxHp;
    public float RemainingTime => Mathf.Max(0f, (float)(_phaseEndTime - NetworkTime.time));

    // [호스트 . 클라] (이전 단계, 다음 단계)
    public event Action<RitualPhase, RitualPhase> PhaseChanged;

    private void Awake()
    {
        Instance = this;
        _target = _flagTarget as IDefenseTarget;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // [서버] NetRitualFlow만 호출
    [Server]
    public void ServerSetPhase(RitualPhase phase, double endTime =0)
    {
        _phaseEndTime = endTime;
        ServerCopyHP();
        _phase = phase;
    }

    // [서버] 깃발 HP는 아직 변경 이벤트가 없어서 디펜스 중에만 읽어서 복사 (값이 같으면 전송 x)
    [ServerCallback]
    private void Update()
    {
        if (_phase == RitualPhase.Countdown || _phase == RitualPhase.Active)
        {
            ServerCopyHP();
        }
    }

    [Server]
    private void ServerCopyHP()
    {
        if (_target == null) return;

        _flagHp = _target.CurrentHP;
        _flagMaxHp = _target.MaxHP;
    }

    private void OnPhaseChanged(RitualPhase oldPhase, RitualPhase newPhase)
    {
        PhaseChanged?.Invoke(oldPhase, newPhase);
    }
}
