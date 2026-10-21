using UnityEngine;

// Resolve camera obstruction after the camera pivot and hit/stress feedback have moved.
[DefaultExecutionOrder(100)]
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

    [Header("First Person Wall Protection")]
    [SerializeField, Range(.01f, .1f)] private float _firstPersonNearClip = .05f;
    [SerializeField, Min(0f)] private float _cameraWallPadding = .015f;
    [SerializeField] private LayerMask _cameraObstructionMask = ~0;
    private CharacterController _bodyCollider;
    private float _previousNearClip;
    private bool _nearClipOverridden;
    private readonly Vector3[] _nearPlaneCorners = new Vector3[4];
    private readonly RaycastHit[] _cameraHits = new RaycastHit[32];

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
        _bodyCollider = GetComponent<CharacterController>();
        // Resolve the current character, including PL_1 replacements and inactive renderers.
        var character = transform.Find("CharacterVisual");
        if (character != null) _playerBodyRenderers = character.GetComponentsInChildren<Renderer>(true);
        CurrentViewMode = _startViewMode;
    }

    private void OnEnable()
    {
        if (_inputReader != null) _inputReader.CancelPressed += HandleCancel;
        if (_inputReader != null) _inputReader.ViewTogglePressed += HandleViewToggle;
    }

    private void Start()
    {
        CurrentViewMode = _startViewMode;

        ApplyViewModeImmediate();
    }

    private void Update()
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;

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
        }
        else
        {
            UpdateCameraViewPosition();
        }
        ProtectFirstPersonCamera();
    }

    private void OnDisable()
    {
        if (_inputReader != null) _inputReader.CancelPressed -= HandleCancel;
        if (_inputReader != null) _inputReader.ViewTogglePressed -= HandleViewToggle;
        EndCurrentMode();
        RestoreNearClip();
        if (_playerBodyRenderers != null)
            foreach (var body in _playerBodyRenderers) if (body != null) body.forceRenderingOff = false;
    }

    // =========================================================
    // View Mode
    // =========================================================

    // PlayerInputReader.ViewTogglePressed (V, rebindable); the reader already skips it while a menu is open or dead
    private void HandleViewToggle()
    {
        if (!PlayerActionGuard.CanAct(gameObject))
        {
            return;
        }

        if (IsBusy)
        {
            return;
        }

        ToggleViewMode();
    }

    public void ToggleViewMode()
    {
        if (_inputReader != null && (!_inputReader.isActiveAndEnabled || _inputReader.GameplayInputBlocked)) return;
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
        UpdateNearClip();

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
        ProtectFirstPersonCamera();
    }

    private void UpdateNearClip()
    {
        if (_playerCamera == null || !PlayerLocalPresentation.IsLocal(gameObject)) return;
        if (CurrentViewMode != ViewMode.FirstPerson)
        {
            RestoreNearClip();
            return;
        }
        if (!_nearClipOverridden)
        {
            _previousNearClip = _playerCamera.nearClipPlane;
            _nearClipOverridden = true;
        }
        // Preserve an already smaller project-specific clip distance.
        _playerCamera.nearClipPlane = Mathf.Min(_previousNearClip, Mathf.Clamp(_firstPersonNearClip, .01f, .1f));
    }

    private void RestoreNearClip()
    {
        if (_nearClipOverridden && _playerCamera != null) _playerCamera.nearClipPlane = _previousNearClip;
        _nearClipOverridden = false;
    }

    private void ProtectFirstPersonCamera()
    {
        UpdateNearClip();
        if (CurrentViewMode != ViewMode.FirstPerson || _playerCamera == null ||
            !_playerCamera.isActiveAndEnabled || !PlayerLocalPresentation.IsLocal(gameObject) ||
            _bodyCollider == null || !_bodyCollider.enabled) return;

        // Protect the near-plane corners as well as the optical centre when looking sideways.
        _playerCamera.CalculateFrustumCorners(new Rect(0f, 0f, 1f, 1f), _playerCamera.nearClipPlane,
            Camera.MonoOrStereoscopicEye.Mono, _nearPlaneCorners);
        float radius = 0f;
        foreach (var corner in _nearPlaneCorners) radius = Mathf.Max(radius, corner.magnitude);
        radius += Mathf.Max(0f, _cameraWallPadding);

        // Start inside the player's collision capsule, not at the offset/shaking camera pivot.
        // Clamp along the capsule axis so crouching and overhead obstacles also use a safe origin.
        var bodyTransform = _bodyCollider.transform;
        var scale = bodyTransform.lossyScale;
        float bodyRadius = _bodyCollider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float halfAxis = Mathf.Max(0f, _bodyCollider.height * Mathf.Abs(scale.y) * .5f - bodyRadius);
        var centre = bodyTransform.TransformPoint(_bodyCollider.center);
        var desired = _playerCamera.transform.position;
        var origin = centre + bodyTransform.up * Mathf.Clamp(Vector3.Dot(desired - centre, bodyTransform.up), -halfAxis, halfAxis);
        var delta = desired - origin;
        float distance = delta.magnitude;
        if (distance <= .0001f) return;
        var direction = delta / distance;
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, _cameraHits, distance,
            _cameraObstructionMask, QueryTriggerInteraction.Ignore);
        // A dense scene must not silently drop the nearest wall when the buffer fills.
        var hits = count == _cameraHits.Length
            ? Physics.SphereCastAll(origin, radius, direction, distance, _cameraObstructionMask, QueryTriggerInteraction.Ignore)
            : _cameraHits;
        if (hits != _cameraHits) count = hits.Length;
        float allowed = distance;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform)) continue;
            allowed = Mathf.Min(allowed, Mathf.Max(0f, hit.distance - .001f));
        }
        // Correct immediately toward the body; lerping through a wall would expose the other side.
        if (allowed < distance) _playerCamera.transform.position = origin + direction * allowed;
    }

    private void ApplyBodyVisibility()
    {
        if (!_hideBodyInFirstPerson)
        {
            return;
        }

        if (_playerBodyRenderers == null) return;
        bool visible = !PlayerLocalPresentation.IsLocal(gameObject) ||
            CurrentViewMode == ViewMode.ThirdPerson;

        foreach (Renderer bodyRenderer in _playerBodyRenderers)
        {
            if (bodyRenderer == null)
            {
                continue;
            }

            bodyRenderer.forceRenderingOff = !visible;
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

    private void HandleCancel()
    {
        if (!IsBusy || GameMenuUI.IsOpen ||
            (_inputReader != null && _inputReader.IsCancelReservedForUI)) return;
        var drawing = GetComponent<PlayerTalismanDrawingController>();
        if (drawing != null && drawing.IsDrawing) return;
        _inputReader?.MarkCancelHandled();
        EndCurrentMode();
    }

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
