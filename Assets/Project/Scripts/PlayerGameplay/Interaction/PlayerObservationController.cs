using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerCameraController))]
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerObservationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;

    [Header("Focus")]
    [SerializeField] private float _focusFov = 35f;
    [SerializeField] private float _rotationSpeed = 12f;
    [SerializeField] private float _fovSpeed = 8f;

    public bool IsObserving { get; private set; }

    private PlayerController _playerController;
    private PlayerCameraController _cameraController;
    private PlayerInteractor _playerInteractor;

    private NPCObservationInteractable _currentTarget;
    private Transform _focusPoint;

    private Quaternion _originalCameraLocalRotation;
    private float _originalFov;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _cameraController = GetComponent<PlayerCameraController>();
        _playerInteractor = GetComponent<PlayerInteractor>();
    }

    private void LateUpdate()
    {
        if (!IsObserving)
        {
            return;
        }

        if (_currentTarget == null ||
            _focusPoint == null ||
            _playerCamera == null)
        {
            EndObservation();
            return;
        }

        UpdateFocus();
    }

    private void OnDisable()
    {
        if (IsObserving)
        {
            EndObservation();
        }
    }

    public void BeginObservation(NPCObservationInteractable target)
    {
        if (IsObserving ||
            target == null ||
            _playerCamera == null)
        {
            return;
        }

        _currentTarget = target;
        _focusPoint = target.FocusPoint;

        if (_focusPoint == null)
        {
            return;
        }

        _originalCameraLocalRotation =
            _playerCamera.transform.localRotation;

        _originalFov =
            _playerCamera.fieldOfView;

        _playerController.CanMove = false;
        _cameraController.CanLook = false;
        _playerInteractor.CanInteract = false;

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

        _playerController.CanMove = true;
        _cameraController.CanLook = true;
        _playerInteractor.CanInteract = true;

        if (_currentTarget != null)
        {
            Debug.Log(
                $"[Observation] 집중 관찰 종료: " +
                $"{_currentTarget.gameObject.name}",
                _currentTarget.gameObject
            );
        }

        _currentTarget = null;
        _focusPoint = null;

        IsObserving = false;
    }

    private void UpdateFocus()
    {
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
                _rotationSpeed * Time.deltaTime
            );

        _playerCamera.fieldOfView =
            Mathf.Lerp(
                _playerCamera.fieldOfView,
                _focusFov,
                _fovSpeed * Time.deltaTime
            );
    }
}