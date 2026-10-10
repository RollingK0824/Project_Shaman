using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerCameraController))]
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerViewModeController : MonoBehaviour
{
    public enum ViewMode
    {
        FirstPerson,
        ThirdPerson
    }

    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _inspectAnchor;

    [Header("View Mode")]
    [SerializeField] private ViewMode _startViewMode = ViewMode.ThirdPerson;

    [SerializeField]
    private Vector3 _firstPersonCameraLocalPosition =
        new Vector3(0f, 0f, 0f);

    [SerializeField]
    private Vector3 _thirdPersonCameraLocalPosition =
        new Vector3(0.45f, 0.15f, -3f);

    [SerializeField] private float _viewTransitionSpeed = 12f;

    [Tooltip("1인칭에서 숨길 플레이어 본체 Renderer")]
    [SerializeField] private Renderer[] _playerBodyRenderers;

    [SerializeField] private bool _hideBodyInFirstPerson = true;

    [Header("Inspect")]
    [SerializeField] private float _inspectRotationSensitivity = 0.2f;

    [Header("Observation")]
    [SerializeField] private float _focusFov = 35f;
    [SerializeField] private float _focusRotationSpeed = 12f;
    [SerializeField] private float _focusFovSpeed = 8f;

    public bool IsInspecting { get; private set; }
    public bool IsObserving { get; private set; }
    public bool IsBusy => IsInspecting || IsObserving;

    public ViewMode CurrentViewMode { get; private set; }

    private PlayerInputReader _inputReader;
    private PlayerController _playerController;
    private PlayerCameraController _cameraController;
    private PlayerInteractor _playerInteractor;
    private PlayerHealth _playerHealth;

    // Inspect
    private InspectInteractable _inspectTarget;
    private GameObject _inspectPreviewObject;
    private Transform _inspectTransform;

    // Observation
    private NPCObservationInteractable _observationTarget;
    private Transform _focusPoint;
    private Quaternion _originalCameraLocalRotation;
    private float _originalFov;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
        _playerController = GetComponent<PlayerController>();
        _cameraController = GetComponent<PlayerCameraController>();
        _playerInteractor = GetComponent<PlayerInteractor>();
        _playerHealth = GetComponent<PlayerHealth>();
    }

    private void Start()
    {
        CurrentViewMode = _startViewMode;

        ApplyViewModeImmediate();
    }

    private void Update()
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        HandleViewToggle();

        if (IsInspecting && _inspectTransform != null)
        {
            RotateInspectTarget();
        }
    }

    private void LateUpdate()
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (IsObserving)
        {
            UpdateObservationFocus();
            return;
        }

        UpdateCameraViewPosition();
    }

    private void OnDisable()
    {
        EndCurrentMode();
    }

    // =========================================================
    // View Mode
    // =========================================================

    private void HandleViewToggle()
    {
        if (IsBusy)
        {
            return;
        }

        if (Keyboard.current == null)
        {
            return;
        }

        if (!Keyboard.current.vKey.wasPressedThisFrame)
        {
            return;
        }

        ToggleViewMode();
    }

    public void ToggleViewMode()
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (CurrentViewMode == ViewMode.FirstPerson)
        {
            CurrentViewMode = ViewMode.ThirdPerson;
        }
        else
        {
            CurrentViewMode = ViewMode.FirstPerson;
        }

        ApplyBodyVisibility();

        Debug.Log(
            $"[ViewMode] {CurrentViewMode}"
        );
    }

    private void UpdateCameraViewPosition()
    {
        if (_playerCamera == null)
        {
            return;
        }

        Vector3 targetPosition =
            CurrentViewMode == ViewMode.FirstPerson
                ? _firstPersonCameraLocalPosition
                : _thirdPersonCameraLocalPosition;

        _playerCamera.transform.localPosition =
            Vector3.Lerp(
                _playerCamera.transform.localPosition,
                targetPosition,
                _viewTransitionSpeed * Time.deltaTime
            );
    }

    private void ApplyViewModeImmediate()
    {
        if (_playerCamera == null)
        {
            return;
        }

        _playerCamera.transform.localPosition =
            CurrentViewMode == ViewMode.FirstPerson
                ? _firstPersonCameraLocalPosition
                : _thirdPersonCameraLocalPosition;

        ApplyBodyVisibility();
    }

    private void ApplyBodyVisibility()
    {
        if (!_hideBodyInFirstPerson)
        {
            return;
        }

        bool visible =
            CurrentViewMode == ViewMode.ThirdPerson;

        foreach (Renderer bodyRenderer in _playerBodyRenderers)
        {
            if (bodyRenderer == null)
            {
                continue;
            }

            bodyRenderer.enabled = visible;
        }
    }

    // =========================================================
    // Inspect
    // =========================================================

    public void BeginInspection(InspectInteractable target)
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (IsBusy ||
            target == null ||
            _inspectAnchor == null)
        {
            return;
        }

        if (target.InspectPreviewPrefab == null)
        {
            Debug.LogWarning(
                $"[Inspect] {target.name}에 Inspect Preview Prefab이 지정되지 않았습니다.",
                target.gameObject
            );

            return;
        }

        _inspectTarget = target;

        _inspectPreviewObject = Instantiate(
            target.InspectPreviewPrefab,
            _inspectAnchor
        );

        _inspectTransform = _inspectPreviewObject.transform;

        DisablePreviewPhysics();
        ApplyInspectPose(target);

        SetGameplayControl(false);

        IsInspecting = true;

        Debug.Log(
            $"[Inspect] 로컬 프리뷰 조사 시작: {target.gameObject.name}",
            target.gameObject
        );
    }

    public void EndInspection()
    {
        if (!IsInspecting)
        {
            return;
        }

        if (_inspectPreviewObject != null)
        {
            Destroy(_inspectPreviewObject);
        }

        if (_inspectTarget != null)
        {
            Debug.Log(
                $"[Inspect] 상세 조사 종료: {_inspectTarget.gameObject.name}",
                _inspectTarget.gameObject
            );
        }

        _inspectTarget = null;
        _inspectPreviewObject = null;
        _inspectTransform = null;

        IsInspecting = false;

        RestoreGameplayControlIfAlive();
    }

    private void ApplyInspectPose(InspectInteractable target)
    {
        if (_inspectTransform == null)
        {
            return;
        }

        _inspectTransform.localPosition =
            target.InspectLocalPosition;

        _inspectTransform.localRotation =
            Quaternion.Euler(target.InspectLocalRotation);

        _inspectTransform.localScale =
            target.InspectLocalScale;
    }

    private void DisablePreviewPhysics()
    {
        if (_inspectPreviewObject == null)
        {
            return;
        }

        Collider[] colliders =
            _inspectPreviewObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider targetCollider in colliders)
        {
            targetCollider.enabled = false;
        }

        Rigidbody[] rigidbodies =
            _inspectPreviewObject.GetComponentsInChildren<Rigidbody>(true);

        foreach (Rigidbody targetRigidbody in rigidbodies)
        {
            targetRigidbody.isKinematic = true;
            targetRigidbody.useGravity = false;
        }
    }

    private void RotateInspectTarget()
    {
        if (_inspectTransform == null)
        {
            return;
        }

        Vector2 lookInput = _inputReader.LookInput;

        float yaw =
            -lookInput.x * _inspectRotationSensitivity;

        float pitch =
            lookInput.y * _inspectRotationSensitivity;

        _inspectTransform.Rotate(
            Vector3.up,
            yaw,
            Space.World
        );

        _inspectTransform.Rotate(
            Vector3.right,
            pitch,
            Space.Self
        );
    }

    // =========================================================
    // Observation
    // =========================================================

    public void BeginObservation(NPCObservationInteractable target)
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (IsBusy ||
            target == null ||
            _playerCamera == null)
        {
            return;
        }

        Transform focusPoint = target.FocusPoint;

        if (focusPoint == null)
        {
            return;
        }

        _observationTarget = target;
        _focusPoint = focusPoint;

        _originalCameraLocalRotation =
            _playerCamera.transform.localRotation;

        _originalFov =
            _playerCamera.fieldOfView;

        SetGameplayControl(false);

        IsObserving = true;

        Debug.Log(
            $"[Observation] 집중 관찰 시작: {target.gameObject.name}",
            target.gameObject
        );
    }

    public void EndObservation()
    {
        if (!IsObserving)
        {
            return;
        }

        if (_playerCamera != null)
        {
            _playerCamera.transform.localRotation =
                _originalCameraLocalRotation;

            _playerCamera.fieldOfView =
                _originalFov;
        }

        if (_observationTarget != null)
        {
            Debug.Log(
                $"[Observation] 집중 관찰 종료: {_observationTarget.gameObject.name}",
                _observationTarget.gameObject
            );
        }

        _observationTarget = null;
        _focusPoint = null;

        IsObserving = false;

        RestoreGameplayControlIfAlive();
    }

    private void UpdateObservationFocus()
    {
        if (_observationTarget == null ||
            _focusPoint == null ||
            _playerCamera == null)
        {
            EndObservation();
            return;
        }

        Vector3 direction =
            _focusPoint.position -
            _playerCamera.transform.position;

        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized,
                Vector3.up
            );

        _playerCamera.transform.rotation =
            Quaternion.Slerp(
                _playerCamera.transform.rotation,
                targetRotation,
                _focusRotationSpeed * Time.deltaTime
            );

        _playerCamera.fieldOfView =
            Mathf.Lerp(
                _playerCamera.fieldOfView,
                _focusFov,
                _focusFovSpeed * Time.deltaTime
            );
    }

    // =========================================================
    // Common
    // =========================================================

    public void EndCurrentMode()
    {
        if (IsInspecting)
        {
            EndInspection();
            return;
        }

        if (IsObserving)
        {
            EndObservation();
        }
    }

    private void SetGameplayControl(bool enabled)
    {
        _playerController.CanMove = enabled;
        _cameraController.CanLook = enabled;
        _playerInteractor.CanInteract = enabled;
    }

    private void RestoreGameplayControlIfAlive()
    {
        if (_playerHealth != null &&
            _playerHealth.IsDead)
        {
            return;
        }

        SetGameplayControl(true);
    }
}