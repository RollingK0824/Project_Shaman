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

    [Header("Animation")]
    [SerializeField] private Animator _animator;

    public bool CanMove { get; set; } = true;
    public bool CanSprint { get; set; } = true;

    private CharacterController _characterController;
    private PlayerInputReader _inputReader;

    private float _verticalVelocity;

    // Starter Assets Animator parameter
    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int MotionSpeedHash =
        Animator.StringToHash("MotionSpeed");

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        // 네트워크 플레이 중일 때만 Local Player 여부를 검사
        if (NetworkClient.active && !isLocalPlayer)
        {
            return;
        }

        HandleMovement();
    }

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

        bool isMoving = moveInput.sqrMagnitude > 0.01f;

        bool isSprinting =
            CanMove &&
            CanSprint &&
            _inputReader.SprintHeld &&
            isMoving;

        float moveSpeed = isSprinting
            ? _sprintSpeed
            : _walkSpeed;

        Vector3 velocity = moveDirection * moveSpeed;

        if (_characterController.isGrounded &&
            _verticalVelocity < 0f)
        {
            _verticalVelocity = _groundedForce;
        }
        else
        {
            _verticalVelocity += _gravity * Time.deltaTime;
        }

        velocity.y = _verticalVelocity;

        _characterController.Move(
            velocity * Time.deltaTime
        );

        UpdateAnimation(isMoving, isSprinting, moveInput);
    }

    private void UpdateAnimation(
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
}