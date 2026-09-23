using System;
using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _playerCamera;

    [Header("Interaction")]
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private LayerMask _interactionMask = ~0;

    public IInteractable CurrentTarget { get; private set; }
    public bool CanInteract { get; set; } = true;

    public event Action<IInteractable> TargetChanged;

    private PlayerInputReader _inputReader;
    private PlayerViewModeController _viewModeController;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
        _viewModeController = GetComponent<PlayerViewModeController>();
    }

    private void OnEnable()
    {
        _inputReader.InteractPressed += TryInteract;
    }

    private void Update()
    {
        UpdateTarget();
    }

    private void OnDisable()
    {
        if (_inputReader != null)
        {
            _inputReader.InteractPressed -= TryInteract;
        }
    }

    private void UpdateTarget()
    {
        if (!CanInteract)
        {
            SetCurrentTarget(null);
            return;
        }

        SetCurrentTarget(FindInteractable());
    }

    private void SetCurrentTarget(IInteractable newTarget)
    {
        if (ReferenceEquals(CurrentTarget, newTarget))
        {
            return;
        }

        CurrentTarget = newTarget;
        TargetChanged?.Invoke(CurrentTarget);
    }

    private IInteractable FindInteractable()
    {
        if (_playerCamera == null)
        {
            return null;
        }

        Ray ray = new Ray(
            _playerCamera.transform.position,
            _playerCamera.transform.forward
        );

        if (!Physics.Raycast(
                ray,
                out RaycastHit hit,
                _interactionDistance,
                _interactionMask,
                QueryTriggerInteraction.Ignore))
        {
            return null;
        }

        return hit.collider.GetComponentInParent<IInteractable>();
    }

    private void TryInteract()
    {
        if (_viewModeController != null && _viewModeController.IsBusy)
        {
            _viewModeController.EndCurrentMode();
            return;
        }

        if (!CanInteract)
        {
            return;
        }

        if (CurrentTarget == null)
        {
            return;
        }

        if (!CurrentTarget.CanInteract(gameObject))
        {
            return;
        }

        CurrentTarget.Interact(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerCamera == null)
        {
            return;
        }

        Gizmos.DrawRay(
            _playerCamera.transform.position,
            _playerCamera.transform.forward * _interactionDistance
        );
    }
}
