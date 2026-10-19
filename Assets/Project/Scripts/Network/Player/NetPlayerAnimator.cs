using Mirror;
using UnityEngine;

// [로컬 클라]       PlayerController·PlayerLocomotionDirection이 넣은 베이스 레이어 파라미터를 읽어 SyncVar에 기록
// [서버]           Sync Direction = Client To Server라 소유 클라 값을 받아 다른 클라에 그대로 전달
// [다른 클라, 호스트] 받은 값을 Animator에 넣기만 함. 상태 전이는 Animator가 파라미터 조건으로 스스로 진행
// 상체 아이템 레이어(ItemHoldType, 레이어 가중치)는 건드리지 않음 → 원격은 NetPlayerEquipment가 ItemHoldDriver를 직접 구동
// Mirror NetworkAnimator 대신 사용: 스크립트 CrossFade로만 움직이는 상체 레이어를 NetworkAnimator가 덮어쓰지 않게 하기 위함
[RequireComponent(typeof(NetworkIdentity))]
public class NetPlayerAnimator : NetworkBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _floatDampTime = .1f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int MotionSpeedHash = Animator.StringToHash("MotionSpeed");
    private static readonly int MoveXHash = Animator.StringToHash("MoveX");
    private static readonly int MoveYHash = Animator.StringToHash("MoveY");
    private static readonly int TurnHash = Animator.StringToHash("Turn");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int FreeFallHash = Animator.StringToHash("FreeFall");
    private static readonly int CrouchHash = Animator.StringToHash("Crouch");

    // 파라미터를 추가하면 여기와 Write/Apply 두 곳에 같이 추가
    [SyncVar] private float _speed;
    [SyncVar] private float _motionSpeed = 1f;
    [SyncVar] private float _moveX;
    [SyncVar] private float _moveY;
    [SyncVar] private float _turn;
    [SyncVar] private bool _grounded = true;
    [SyncVar] private bool _jump;
    [SyncVar] private bool _freeFall;
    [SyncVar] private bool _crouch;

    private void Awake()
    {
        if (_animator == null) _animator = transform.Find("CharacterVisual")?.GetComponentInChildren<Animator>(true);
    }

    private void LateUpdate()
    {
        if (!isClient && !isServer) return;
        if (_animator == null || !_animator.isActiveAndEnabled) return;

        if (isOwned) WriteFromAnimator();
        else ApplyToAnimator();
    }

    // [로컬 클라] 소수 둘째 자리까지만 기록해서 감쇠 중 미세한 변화로 매번 전송되는 것 방지
    private void WriteFromAnimator()
    {
        _speed = Round(_animator.GetFloat(SpeedHash));
        _motionSpeed = Round(_animator.GetFloat(MotionSpeedHash));
        _moveX = Round(_animator.GetFloat(MoveXHash));
        _moveY = Round(_animator.GetFloat(MoveYHash));
        _turn = Round(_animator.GetFloat(TurnHash));
        _grounded = _animator.GetBool(GroundedHash);
        _jump = _animator.GetBool(JumpHash);
        _freeFall = _animator.GetBool(FreeFallHash);
        _crouch = _animator.GetBool(CrouchHash);
    }

    // [다른 클라, 호스트] 실수값은 감쇠를 걸어 전송 간격 사이를 부드럽게 채움
    private void ApplyToAnimator()
    {
        float dt = Time.deltaTime;
        _animator.SetFloat(SpeedHash, _speed, _floatDampTime, dt);
        _animator.SetFloat(MotionSpeedHash, _motionSpeed, _floatDampTime, dt);
        _animator.SetFloat(MoveXHash, _moveX, _floatDampTime, dt);
        _animator.SetFloat(MoveYHash, _moveY, _floatDampTime, dt);
        _animator.SetFloat(TurnHash, _turn, _floatDampTime, dt);
        _animator.SetBool(GroundedHash, _grounded);
        _animator.SetBool(JumpHash, _jump);
        _animator.SetBool(FreeFallHash, _freeFall);
        _animator.SetBool(CrouchHash, _crouch);
    }

    private static float Round(float value) => Mathf.Round(value * 100f) * .01f;
}
