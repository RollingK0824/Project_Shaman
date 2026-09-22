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
    private PlayerInspectController _inspectController;
    private PlayerObservationController _observationController;

    private void Awake()
    {
        _inputReader =
            GetComponent<PlayerInputReader>();

        _inspectController =
            GetComponent<PlayerInspectController>();

        _observationController =
            GetComponent<PlayerObservationController>();
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

        IInteractable newTarget =
            FindInteractable();

        SetCurrentTarget(newTarget);
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

        return hit.collider
            .GetComponentInParent<IInteractable>();
    }

    private void TryInteract()
    {
        // 오브젝트 상세 조사 중이라면
        // F를 다시 눌러 조사 종료
        if (_inspectController != null &&
            _inspectController.IsInspecting)
        {
            _inspectController.EndInspection();
            return;
        }

        // NPC 집중 관찰 중이라면
        // F를 다시 눌러 관찰 종료
        if (_observationController != null &&
            _observationController.IsObserving)
        {
            _observationController.EndObservation();
            return;
        }

        if (!CanInteract)
        {
            return;
        }

        if (CurrentTarget == null)
        {
            Debug.Log(
                "[Interaction] 상호작용 가능한 대상이 없습니다."
            );

            return;
        }

        if (!CurrentTarget.CanInteract(gameObject))
        {
            Debug.Log(
                $"[Interaction] 현재 상호작용할 수 없습니다: " +
                $"{CurrentTarget.InteractionPrompt}"
            );

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
            _playerCamera.transform.forward *
            _interactionDistance
        );
    }
}