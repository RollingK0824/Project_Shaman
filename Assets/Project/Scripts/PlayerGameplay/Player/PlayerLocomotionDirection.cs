using Mirror;
using UnityEngine;

// Presentation only: supplies direction/turn parameters alongside PlayerController's
// existing Speed/Grounded/Jump parameters. Never moves the player or applies root motion.
[DisallowMultipleComponent]
public class PlayerLocomotionDirection : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField, Min(0f)] private float _blendTime = .12f;
    [SerializeField, Min(0f)] private float _turnDeadZone = 15f;
    private CharacterController _controller;
    private PlayerController _player;
    private PlayerCrouch _crouch;
    private bool _hasCrouch;
    private float _previousYaw;
    private static readonly int MoveX = Animator.StringToHash("MoveX");
    private static readonly int MoveY = Animator.StringToHash("MoveY");
    private static readonly int Turn = Animator.StringToHash("Turn");
    private static readonly int Crouch = Animator.StringToHash("Crouch");

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _player = GetComponent<PlayerController>();
        _crouch = GetComponent<PlayerCrouch>();
        if (_animator == null) _animator = transform.Find("CharacterVisual")?.GetComponentInChildren<Animator>(true);
        if (_animator != null) foreach (var p in _animator.parameters) if (p.nameHash == Crouch) _hasCrouch = true;
    }
    private void OnEnable() => _previousYaw = transform.eulerAngles.y;
    private void LateUpdate()
    {
        float yaw = transform.eulerAngles.y;
        float delta = Mathf.DeltaAngle(_previousYaw, yaw);
        _previousYaw = yaw;
        if (_animator == null || !_animator.isActiveAndEnabled || Time.deltaTime <= 0f) return;
        // Remote parameters belong to the existing NetworkAnimator, not local inference.
        if ((NetworkClient.active || NetworkServer.active) && (_player == null || !_player.isLocalPlayer)) return;
        bool canMove = _player != null && _player.CanMove && PlayerActionGuard.CanAct(gameObject);
        Vector3 velocity = canMove && _controller != null && _controller.enabled
            ? transform.InverseTransformDirection(_controller.velocity) : Vector3.zero;
        Vector2 direction = new Vector2(velocity.x, velocity.z);
        direction = direction.sqrMagnitude > .01f ? direction.normalized : Vector2.zero;
        float turn = canMove && direction == Vector2.zero ? delta / Time.deltaTime : 0f;
        if (Mathf.Abs(turn) < _turnDeadZone) turn = 0f;
        _animator.SetFloat(MoveX, direction.x, _blendTime, Time.deltaTime);
        _animator.SetFloat(MoveY, direction.y, _blendTime, Time.deltaTime);
        _animator.SetFloat(Turn, Mathf.Clamp(turn, -180f, 180f), _blendTime, Time.deltaTime);
        if (_hasCrouch) _animator.SetBool(Crouch, canMove && _crouch != null && _crouch.IsCrouching);
    }
}
