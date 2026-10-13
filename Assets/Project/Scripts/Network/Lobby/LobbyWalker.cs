using UnityEngine;


[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
public class LobbyWalker : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2.5f;
    [SerializeField] private float _gravity = -15f;

    private CharacterController _controller;
    private PlayerInputReader _input;
    private float _verticalVelocity;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputReader>();
    }

    private void Update()
    {
        Vector2 move = _input.MoveInput;
        Vector3 direction = transform.right * move.x + transform.forward * move.y;

        if (_controller.isGrounded && _verticalVelocity < 0f) _verticalVelocity = -2f;
        _verticalVelocity += _gravity * Time.deltaTime;

        Vector3 velocity = direction * _moveSpeed + Vector3.up * _verticalVelocity;
        _controller.Move(velocity * Time.deltaTime);
    }

    // 로비를 떠날 때(메인 메뉴 복귀 등) 커서를 돌려줌. 게임 씬에서는 NetPlayer가 다시 잠금
    private void OnDestroy()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
