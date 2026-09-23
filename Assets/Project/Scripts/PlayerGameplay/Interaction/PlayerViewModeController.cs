using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerCameraController))]
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerViewModeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private Transform _inspectAnchor;

    [Header("Inspect")]
    [SerializeField] private float _inspectRotationSensitivity = 0.2f;

    [Header("Observation")]
    [SerializeField] private float _focusFov = 35f;
    [SerializeField] private float _focusRotationSpeed = 12f;
    [SerializeField] private float _focusFovSpeed = 8f;

    public bool IsInspecting { get; private set; }
    public bool IsObserving { get; private set; }
    public bool IsBusy => IsInspecting || IsObserving;

    private PlayerInputReader _inputReader;
    private PlayerController _playerController;
    private PlayerCameraController _cameraController;
    private PlayerInteractor _playerInteractor;
    private PlayerHealth _playerHealth;

    private InspectInteractable _inspectTarget;
    private Transform _inspectTransform;
    private GameObject _inspectPreviewObject;
    private bool _usingPreviewObject;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;
    private Collider[] _targetColliders;
    private bool[] _colliderStates;
    private Rigidbody _targetRigidbody;
    private bool _originalIsKinematic;
    private bool _originalUseGravity;

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

    private void Update()
    {
        if (IsInspecting && _inspectTransform != null)
        {
            RotateInspectTarget();
        }
    }

    private void LateUpdate()
    {
        if (IsObserving)
        {
            UpdateObservationFocus();
        }
    }

    private void OnDisable()
    {
        EndCurrentMode();
    }

    public void BeginInspection(InspectInteractable target)
    {
        if (IsBusy || target == null || _inspectAnchor == null)
        {
            return;
        }

        _inspectTarget = target;

        if (target.InspectPreviewPrefab != null)
        {
            _inspectPreviewObject = Instantiate(
                target.InspectPreviewPrefab,
                _inspectAnchor
            );

            _inspectTransform = _inspectPreviewObject.transform;
            _usingPreviewObject = true;
        }
        else
        {
            // 프로토타입 호환용 fallback.
            // 멀티플레이에서는 InspectPreviewPrefab 사용을 권장한다.
            _inspectTransform = target.transform;
            _usingPreviewObject = false;

            SaveOriginalTransform();
            SaveAndDisablePhysics();
            _inspectTransform.SetParent(_inspectAnchor, false);
        }

        ApplyInspectPose(target);
        SetGameplayControl(false);
        IsInspecting = true;

        Debug.Log(
            $"[Inspect] 상세 조사 시작: {target.gameObject.name}",
            target.gameObject
        );
    }

    public void EndInspection()
    {
        if (!IsInspecting)
        {
            return;
        }

        if (_usingPreviewObject)
        {
            if (_inspectPreviewObject != null)
            {
                Destroy(_inspectPreviewObject);
            }
        }
        else
        {
            RestoreOriginalTransform();
            RestorePhysics();
        }

        if (_inspectTarget != null)
        {
            Debug.Log(
                $"[Inspect] 상세 조사 종료: {_inspectTarget.gameObject.name}",
                _inspectTarget.gameObject
            );
        }

        _inspectTarget = null;
        _inspectTransform = null;
        _inspectPreviewObject = null;
        _usingPreviewObject = false;
        IsInspecting = false;

        RestoreGameplayControlIfAlive();
    }

    public void BeginObservation(NPCObservationInteractable target)
    {
        if (IsBusy || target == null || _playerCamera == null)
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
        _originalCameraLocalRotation = _playerCamera.transform.localRotation;
        _originalFov = _playerCamera.fieldOfView;

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
            _playerCamera.transform.localRotation = _originalCameraLocalRotation;
            _playerCamera.fieldOfView = _originalFov;
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
        if (_playerHealth != null && _playerHealth.IsDead)
        {
            return;
        }

        SetGameplayControl(true);
    }

    private void ApplyInspectPose(InspectInteractable target)
    {
        if (_inspectTransform == null)
        {
            return;
        }

        _inspectTransform.localPosition = target.InspectLocalPosition;
        _inspectTransform.localRotation = Quaternion.Euler(target.InspectLocalRotation);
        _inspectTransform.localScale = target.InspectLocalScale;
    }

    private void RotateInspectTarget()
    {
        Vector2 lookInput = _inputReader.LookInput;

        float yaw = -lookInput.x * _inspectRotationSensitivity;
        float pitch = lookInput.y * _inspectRotationSensitivity;

        _inspectTransform.Rotate(Vector3.up, yaw, Space.World);
        _inspectTransform.Rotate(Vector3.right, pitch, Space.Self);
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

        Vector3 direction = _focusPoint.position - _playerCamera.transform.position;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized,
            Vector3.up
        );

        _playerCamera.transform.rotation = Quaternion.Slerp(
            _playerCamera.transform.rotation,
            targetRotation,
            _focusRotationSpeed * Time.deltaTime
        );

        _playerCamera.fieldOfView = Mathf.Lerp(
            _playerCamera.fieldOfView,
            _focusFov,
            _focusFovSpeed * Time.deltaTime
        );
    }

    private void SaveOriginalTransform()
    {
        _originalParent = _inspectTransform.parent;
        _originalLocalPosition = _inspectTransform.localPosition;
        _originalLocalRotation = _inspectTransform.localRotation;
        _originalLocalScale = _inspectTransform.localScale;
    }

    private void RestoreOriginalTransform()
    {
        if (_inspectTransform == null)
        {
            return;
        }

        _inspectTransform.SetParent(_originalParent, false);
        _inspectTransform.localPosition = _originalLocalPosition;
        _inspectTransform.localRotation = _originalLocalRotation;
        _inspectTransform.localScale = _originalLocalScale;
    }

    private void SaveAndDisablePhysics()
    {
        _targetColliders = _inspectTransform.GetComponentsInChildren<Collider>(true);
        _colliderStates = new bool[_targetColliders.Length];

        for (int i = 0; i < _targetColliders.Length; i++)
        {
            _colliderStates[i] = _targetColliders[i].enabled;
            _targetColliders[i].enabled = false;
        }

        _targetRigidbody = _inspectTransform.GetComponent<Rigidbody>();
        if (_targetRigidbody == null)
        {
            return;
        }

        _originalIsKinematic = _targetRigidbody.isKinematic;
        _originalUseGravity = _targetRigidbody.useGravity;
        _targetRigidbody.isKinematic = true;
        _targetRigidbody.useGravity = false;
    }

    private void RestorePhysics()
    {
        if (_targetColliders != null && _colliderStates != null)
        {
            int count = Mathf.Min(_targetColliders.Length, _colliderStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (_targetColliders[i] != null)
                {
                    _targetColliders[i].enabled = _colliderStates[i];
                }
            }
        }

        if (_targetRigidbody != null)
        {
            _targetRigidbody.isKinematic = _originalIsKinematic;
            _targetRigidbody.useGravity = _originalUseGravity;
        }

        _targetColliders = null;
        _colliderStates = null;
        _targetRigidbody = null;
    }
}
