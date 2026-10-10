using UnityEngine;
using Mirror;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;
    [SerializeField] private float _sprintSpeed = 6f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private float _groundedForce = -2f;

    [Header("Jump")]
    [SerializeField] private float _jumpHeight = 1.2f;

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    public bool CanMove { get; set; } = true;
    public bool CanSprint { get; set; } = true;

    private CharacterController _characterController;
    private PlayerInputReader _inputReader;

    private float _verticalVelocity;
    private bool _jumpQueued;

    // =========================================================
    // Starter Assets Animator Parameters
    // =========================================================

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int MotionSpeedHash =
        Animator.StringToHash("MotionSpeed");

    private static readonly int GroundedHash =
        Animator.StringToHash("Grounded");

    private static readonly int JumpHash =
        Animator.StringToHash("Jump");

    private static readonly int FreeFallHash =
        Animator.StringToHash("FreeFall");

    // =========================================================
    // Unity
    // =========================================================

    private void Awake()
    {
        _characterController =
            GetComponent<CharacterController>();

        _inputReader =
            GetComponent<PlayerInputReader>();
    }

    private void OnEnable()
    {
        if (_inputReader != null)
        {
            _inputReader.JumpPressed +=
                OnJumpPressed;
        }
    }

    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.JumpPressed -=
                OnJumpPressed;
        }
    }

    private void Update()
    {
        // 네트워크 플레이 중에는
        // Local Player만 입력과 이동을 처리한다.
        if ((NetworkClient.active || NetworkServer.active) &&
            !isLocalPlayer)
        {
            return;
        }

        if (!PlayerActionGuard.CanAct(gameObject))
        {
            _jumpQueued = false;
            UpdateMovementAnimation(false, false, Vector2.zero);
            return;
        }
        HandleMovement();
    }

    // =========================================================
    // Jump Input
    // =========================================================

    private void OnJumpPressed()
    {
        _jumpQueued = true;
    }

    // =========================================================
    // Movement
    // =========================================================

    private void HandleMovement()
    {
        Vector2 moveInput = CanMove
            ? _inputReader.MoveInput
            : Vector2.zero;

        Vector3 moveDirection =
            transform.right * moveInput.x +
            transform.forward * moveInput.y;

        if (moveDirection.sqrMagnitude > 1f)
        {
            moveDirection.Normalize();
        }

        bool isMoving =
            moveInput.sqrMagnitude > 0.01f;

        bool isSprinting =
            CanMove &&
            CanSprint &&
            _inputReader.SprintHeld &&
            isMoving;

        float moveSpeed = isSprinting
            ? _sprintSpeed
            : _walkSpeed;

        Vector3 velocity =
            moveDirection * moveSpeed;

        bool isGrounded =
            _characterController.isGrounded;

        // -----------------------------------------------------
        // Ground
        // -----------------------------------------------------

        if (isGrounded &&
            _verticalVelocity < 0f)
        {
            // 바닥에 붙어 있도록 약간의 하강 속도 유지
            _verticalVelocity =
                _groundedForce;
        }

        // -----------------------------------------------------
        // Jump
        // -----------------------------------------------------

        bool jumpRequested =
            _jumpQueued;

        // 입력은 한 번만 소비
        _jumpQueued = false;

        bool jumpStarted = false;

        if (CanMove &&
            isGrounded &&
            jumpRequested)
        {
            _verticalVelocity =
                Mathf.Sqrt(
                    _jumpHeight *
                    -2f *
                    _gravity
                );

            jumpStarted = true;
        }

        // -----------------------------------------------------
        // Gravity
        // -----------------------------------------------------

        _verticalVelocity +=
            _gravity * Time.deltaTime;

        velocity.y =
            _verticalVelocity;

        // -----------------------------------------------------
        // Move
        // -----------------------------------------------------

        CollisionFlags collisionFlags =
    _characterController.Move(
        velocity * Time.deltaTime
    );

        bool isGroundedAfterMove =
    (collisionFlags & CollisionFlags.Below) != 0 ||
    _characterController.isGrounded;

        if (isGroundedAfterMove &&
    _verticalVelocity < 0f)
        {
            _verticalVelocity =
                _groundedForce;
        }
        // -----------------------------------------------------
        // Animation
        // -----------------------------------------------------

        UpdateMovementAnimation(
            isMoving,
            isSprinting,
            moveInput
        );

        UpdateJumpAnimation(
            jumpStarted,
            isGroundedAfterMove
        );
    }

    // =========================================================
    // Movement Animation
    // =========================================================

    private void UpdateMovementAnimation(
        bool isMoving,
        bool isSprinting,
        Vector2 moveInput)
    {
        if (_animator == null)
        {
            return;
        }

        float animationSpeed = 0f;

        if (isMoving)
        {
            animationSpeed = isSprinting
                ? _sprintSpeed
                : _walkSpeed;
        }

        _animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.1f,
            Time.deltaTime
        );

        _animator.SetFloat(
            MotionSpeedHash,
            moveInput.magnitude
        );
    }

    // =========================================================
    // Jump Animation
    // =========================================================

    private void UpdateJumpAnimation(
    bool jumpStarted,
    bool isGrounded)
    {
        if (_animator == null)
        {
            return;
        }

        bool isFalling =
            !isGrounded &&
            _verticalVelocity < 0f;

        _animator.SetBool(
            GroundedHash,
            isGrounded
        );

        _animator.SetBool(
            JumpHash,
            jumpStarted
        );

        _animator.SetBool(
            FreeFallHash,
            isFalling
        );
    }
}