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

    public event Action<IInteractable> TargetChanged;

    private PlayerInputReader _inputReader;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
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
        IInteractable newTarget = FindInteractable();

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
        if (CurrentTarget == null)
        {
            Debug.Log("[Interaction] 상호작용 가능한 대상이 없습니다.");
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
            _playerCamera.transform.forward * _interactionDistance
        );
    }
}