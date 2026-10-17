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
    [Tooltip("Clip reference speeds, measured from the matching root-motion clips. Gameplay still uses in-place animation.")]
    [SerializeField, Min(.1f)] private float _walkAnimationSpeed = 1.9f;
    [SerializeField, Min(.1f)] private float _runAnimationSpeed = 3.8f;
    [Tooltip("Keep these thresholds matched to the Animator Speed blend tree.")]
    [SerializeField, Min(.1f)] private float _walkBlendSpeed = 2f;
    [SerializeField, Min(.1f)] private float _runBlendSpeed = 5f;

    public bool CanMove { get; set; } = true;
    public bool CanSprint { get; set; } = true;

    private CharacterController _characterController;
    private PlayerInputReader _inputReader;
    private PlayerCrouch _crouch;
    private PlayerTalismanEffectController _talismanEffects;

    private float _verticalVelocity;
    private bool _jumpQueued;
    private bool _jumpInProgress;
    private float _airborneTime;

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
        // Older scene instances can override the prefab's Animator reference with None.
        // Resolve only the character visual, so held-item Animators cannot be selected.
        if (_animator == null)
        {
            Transform characterVisual = transform.Find("CharacterVisual");
            if (characterVisual != null)
            {
                _animator = characterVisual.GetComponentInChildren<Animator>(true);
            }
        }

        _crouch = GetComponent<PlayerCrouch>();
        _talismanEffects = GetComponent<PlayerTalismanEffectController>();
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
        bool inputAllowed = _inputReader.isActiveAndEnabled && !_inputReader.GameplayInputBlocked;
        if (!inputAllowed) _jumpQueued = false;
        Vector2 moveInput = CanMove && inputAllowed
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
            (_crouch == null || !_crouch.IsCrouching) &&
            _inputReader.SprintHeld &&
            isMoving;

        float moveSpeed = isSprinting
            ? _sprintSpeed
            : _walkSpeed;
        if (_crouch != null) moveSpeed *= _crouch.SpeedMultiplier;
        if (_talismanEffects != null) moveSpeed *= _talismanEffects.MovementMultiplier;

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
            inputAllowed && (_crouch == null || !_crouch.IsCrouching) &&
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

        // Collision-resolved motion: pushing into a wall must not keep the feet running.
        Vector3 velocity = _characterController.enabled ? _characterController.velocity : Vector3.zero;
        float animationSpeed = isMoving ? new Vector2(velocity.x, velocity.z).magnitude : 0f;

        _animator.SetFloat(
            SpeedHash,
            animationSpeed,
            0.1f,
            Time.deltaTime
        );

        float referenceSpeed = Mathf.Lerp(_walkAnimationSpeed, _runAnimationSpeed,
            Mathf.InverseLerp(_walkBlendSpeed, _runBlendSpeed, animationSpeed));
        _animator.SetFloat(
            MotionSpeedHash,
            animationSpeed < .1f ? 1f : Mathf.Clamp(animationSpeed / Mathf.Max(.1f, referenceSpeed), .55f, 1.8f),
            .12f, Time.deltaTime
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

        if (jumpStarted) _jumpInProgress = true;
        if (isGrounded && !jumpStarted) { _jumpInProgress = false; _airborneTime = 0f; }
        else _airborneTime += Time.deltaTime;
        bool isFalling = !isGrounded && _verticalVelocity < 0f && _airborneTime > .1f;

        _animator.SetBool(
            GroundedHash,
            isGrounded && !jumpStarted
        );

        _animator.SetBool(
            JumpHash,
            _jumpInProgress && !isFalling
        );

        _animator.SetBool(
            FreeFallHash,
            isFalling
        );
    }
}
