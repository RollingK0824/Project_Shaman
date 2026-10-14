using UnityEngine;


[RequireComponent(typeof(CharacterController), typeof(PlayerInputReader))]
public class LobbyWalker : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 2.5f;
    [SerializeField] private float _gravity = -15f;

    private CharacterController _controller;
    private PlayerInputReader _input;
    private float _verticalVelocity;

    private PlayerInteractor _interactor;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _input = GetComponent<PlayerInputReader>();

        _interactor = GetComponent<PlayerInteractor>();
    }

    private void OnEnable()
    {
        _input.UseItemStarted += ClickInteract;
    }

    private void OnDisable()
    {
        if (_input != null) _input.UseItemStarted -= ClickInteract;
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

    // [로컬] 좌클릭으로 바라보는 대상과 상호작용 (F와 같은 동작)
    private void ClickInteract()
    {
        IInteractable target = _interactor != null ? _interactor.CurrentTarget : null;
        if (target != null && target.CanInteract(gameObject)) target.Interact(gameObject);
    }
}
