using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInputReader))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _walkSpeed = 4f;
    [SerializeField] private float _sprintSpeed = 6f;

    [Header("Gravity")]
    [SerializeField] private float _gravity = -20f;
    [SerializeField] private float _groundedForce = -2f;

    public bool CanMove { get; set; } = true;
    public bool CanSprint { get; set; } = true;

    private CharacterController _characterController;
    private PlayerInputReader _inputReader;

    private float _verticalVelocity;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();
        _inputReader = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
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

        bool isSprinting =
            CanMove &&
            CanSprint &&
            _inputReader.SprintHeld &&
            moveInput.sqrMagnitude > 0f;

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
    }
}