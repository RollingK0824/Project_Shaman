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

    private IInteractable _currentTarget;

    public IInteractable CurrentTarget =>
        _currentTarget is UnityEngine.Object obj && obj == null ? null : _currentTarget;
    public bool CanInteract { get; set; } = true;

    public event Action<IInteractable> TargetChanged;

    private PlayerInputReader _inputReader;
    private PlayerViewModeController _viewModeController;
    private PlayerLanternPlacement _lanternPlacement;

    private void Awake()
    {
        _lanternPlacement = GetComponent<PlayerLanternPlacement>();
        if (_lanternPlacement == null) _lanternPlacement = gameObject.AddComponent<PlayerLanternPlacement>();
        _inputReader =
            GetComponent<PlayerInputReader>();

        _viewModeController =
            GetComponent<PlayerViewModeController>();
    }

    private void OnEnable()
    {
        _inputReader.InteractPressed +=
            TryInteract;
    }

    private void Update()
    {
        UpdateTarget();
    }

    private void OnDisable()
    {
        SetCurrentTarget(null);
        if (_inputReader != null)
        {
            _inputReader.InteractPressed -=
                TryInteract;
        }
    }

    private void UpdateTarget()
    {
        if (!CanInteract || _inputReader.GameplayInputBlocked || !PlayerActionGuard.CanAct(gameObject))
        {
            SetCurrentTarget(null);
            return;
        }

        var target = FindInteractable();
        if (target == null && _lanternPlacement != null && _lanternPlacement.Target.CanInteract(gameObject))
            target = _lanternPlacement.Target;
        SetCurrentTarget(target);
    }

    private void SetCurrentTarget(
        IInteractable newTarget)
    {
        if (ReferenceEquals(
                _currentTarget,
                newTarget))
        {
            return;
        }

        _currentTarget = newTarget;

        TargetChanged?.Invoke(
            CurrentTarget
        );
    }

    private IInteractable FindInteractable()
    {
        if (_playerCamera == null)
        {
            return null;
        }

        if (_viewModeController != null && _viewModeController.CurrentViewMode == PlayerViewModeController.ViewMode.ThirdPerson)
            return FindThirdPersonInteractable();

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

        return
            hit.collider
                .GetComponentInParent<IInteractable>();
    }

    private IInteractable FindThirdPersonInteractable()
    {
        var cameraTransform = _playerCamera.transform;
        // The camera boom is not interaction reach. Retain the existing player-side F/request path.
        Vector3 origin = cameraTransform.parent != null ? cameraTransform.parent.position : transform.position + Vector3.up;
        float rayLength = Vector3.Distance(cameraTransform.position, origin) + _interactionDistance;
        var hits = Physics.RaycastAll(cameraTransform.position, cameraTransform.forward, rayLength, _interactionMask, QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
        foreach (var hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)) continue;
            var target = hit.collider.GetComponentInParent<IInteractable>();
            if (target == null || Vector3.Distance(origin, hit.point) > _interactionDistance) return null;
            Vector3 delta = hit.point-origin;
            foreach (var obstruction in Physics.RaycastAll(origin, delta.normalized, delta.magnitude, _interactionMask, QueryTriggerInteraction.Ignore))
            {
                if (obstruction.collider.transform.IsChildOf(transform)) continue;
                if (!ReferenceEquals(obstruction.collider.GetComponentInParent<IInteractable>(), target)) return null;
            }
            return target;
        }
        return null;
    }

    // Items share the existing camera, range and obstruction mask; no second aim controller.
    public bool TryGetAttackRay(out Ray ray, out float reach)
    {
        ray = default; reach = 0f;
        if (!CanInteract || !PlayerActionGuard.CanAct(gameObject) || _playerCamera == null) return false;
        ray = new Ray(_playerCamera.transform.position, _playerCamera.transform.forward);
        reach = _interactionDistance;
        // Clip at the nearest solid surface but let the attack volume detect trigger-only hitboxes.
        if (Physics.Raycast(ray, out var hit, reach, _interactionMask, QueryTriggerInteraction.Ignore))
            reach = Mathf.Min(reach, hit.distance + .01f);
        return reach > 0f;
    }

    public bool TryGetTargetComponent<T>(
        out T target)
        where T : class
    {
        target = null;
        if (!CanInteract || !PlayerActionGuard.CanAct(gameObject)) return false;

        if (_playerCamera == null)
        {
            return false;
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
            return false;
        }

        target =
            hit.collider
                .GetComponentInParent<T>();

        return target != null;
    }

    private void TryInteract()
    {
        if (!PlayerActionGuard.CanAct(gameObject)) return;
        if (_viewModeController != null &&
            _viewModeController.IsBusy)
        {
            _viewModeController.EndCurrentMode();
            return;
        }

        if (!CanInteract || !PlayerActionGuard.CanAct(gameObject))
        {
            return;
        }

        UpdateTarget(); // Revalidate before F; pickup and placement cannot both fire.

        if (CurrentTarget == null)
        {
            return;
        }

        if (!CurrentTarget.CanInteract(
                gameObject))
        {
            return;
        }

        CurrentTarget.Interact(
            gameObject
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (_playerCamera == null)
        {
            return;
        }

        Gizmos.DrawRay(
            _playerCamera.transform.position,
            _playerCamera.transform.forward *
            _interactionDistance
        );
    }
}
