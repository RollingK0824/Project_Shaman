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

    private void Awake()
    {
        _stress = GetComponent<PlayerStressController>();
        _stress.UseExternalAuthority = true;
        _health = GetComponent<PlayerHealth>();
        _status = GetComponent<NetPlayerStatus>();
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
        PlayerEvents.StressRequested += HandleStressRequested;
    }

    public override void OnStopServer()
    {
        PlayerEvents.StressRequested -= HandleStressRequested;
        base.OnStopServer();
    }


    // [서버] 버스로 돌아온 스트레스 요청을 판정. 모든 플레이어의 요청이 오므로 내 것만 처리
    private void HandleStressRequested(PlayerStressController target, float delta, StressCause cause, GameObject source)
    {
        if (target != _stress || float.IsNaN(delta) || float.IsInfinity(delta)) return;
        if (_health != null && _health.IsDead) return;

        GameManager gm = GameManager.Instance;
        if (gm == null || gm.CurrentGameState != GameState.Ongoing) return;

        float next = Mathf.Clamp(_currentStress + delta, 0f, _stress.MaxStress);
        if (Mathf.Approximately(next, _currentStress)) return;

        _stress.SetStress(next);
        _currentStress = next;

        if (_status != null) _status.ServerSetStressLevel(_stress.CurrentLevel);
    }

    private void OnStressChanged(float oldValue, float newValue)
    {
        _stress.SetStress(newValue);
    }
}
