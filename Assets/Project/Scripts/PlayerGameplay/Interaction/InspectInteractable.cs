using UnityEngine;

public class InspectInteractable : MonoBehaviour, IInteractable
{
    [Header("Interaction")]
    [SerializeField] private string _interactionPrompt = "자세히 조사하기";
    [SerializeField] private bool _canInteract = true;

    [Header("Inspect Preview")]
    [Tooltip("멀티플레이에서는 실제 월드 오브젝트 대신 이 로컬 프리뷰 프리팹을 사용합니다.")]
    [SerializeField] private GameObject _inspectPreviewPrefab;

    [Header("Inspect Pose")]
    [SerializeField] private Vector3 _inspectLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 _inspectLocalRotation = Vector3.zero;
    [SerializeField] private Vector3 _inspectLocalScale = Vector3.one;

    public string InteractionPrompt => _interactionPrompt;
    public GameObject InspectPreviewPrefab => _inspectPreviewPrefab;
    public Vector3 InspectLocalPosition => _inspectLocalPosition;
    public Vector3 InspectLocalRotation => _inspectLocalRotation;
    public Vector3 InspectLocalScale => _inspectLocalScale;

    public bool CanInteract(GameObject interactor)
    {
        return _canInteract;
    }

    public void Interact(GameObject interactor)
    {
        PlayerViewModeController viewModeController =
            interactor.GetComponent<PlayerViewModeController>();

        if (viewModeController == null)
        {
            Debug.LogWarning(
                $"[Inspect] {interactor.name}에 PlayerViewModeController가 없습니다.",
                interactor
            );
            return;
        }

        viewModeController.BeginInspection(this);
    }
}
