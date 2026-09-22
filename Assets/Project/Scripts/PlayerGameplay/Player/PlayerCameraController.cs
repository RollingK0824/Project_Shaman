using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _cameraPivot;

    [Header("Look")]
    [SerializeField] private float _mouseSensitivity = 0.08f;
    [SerializeField] private float _minPitch = -85f;
    [SerializeField] private float _maxPitch = 85f;

    public bool CanLook { get; set; } = true;

    private PlayerInputReader _inputReader;
    private float _pitch;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
    }

    private void Start()
    {
        LockCursor();
    }

    private void Update()
    {
        if (!CanLook)
        {
            return;
        }

        HandleLook();
    }

    private void HandleLook()
    {
        if (_cameraPivot == null)
        {
            return;
        }

        Vector2 lookInput = _inputReader.LookInput;

        float yawDelta =
            lookInput.x * _mouseSensitivity;

        float pitchDelta =
            lookInput.y * _mouseSensitivity;

        _pitch -= pitchDelta;

        _pitch = Mathf.Clamp(
            _pitch,
            _minPitch,
            _maxPitch
        );

        // 좌우 회전은 Player 전체를 회전한다.
        transform.Rotate(
            Vector3.up,
            yawDelta,
            Space.Self
        );

        // 상하 회전은 카메라 Pivot만 회전한다.
        _cameraPivot.localRotation =
            Quaternion.Euler(
                _pitch,
                0f,
                0f
            );
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}