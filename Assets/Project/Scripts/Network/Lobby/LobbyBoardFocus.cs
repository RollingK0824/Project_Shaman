using UnityEngine;
using System.Collections;
using UnityEngine.UI;

// [로컬 전용] 로비 게시판 포커스
// 상호작용하면 마케라가 게시판 정면으로 이동하고, 커서로 월드 UI를 조작. ESC/돌아가기로 복귀
[RequireComponent(typeof(Collider))]
public class LobbyBoardFocus : MonoBehaviour, IInteractable
{


    [SerializeField] private Transform _focusPoint;            // 게시판이 화면을 채우는 카메라 위치
    [SerializeField] private GraphicRaycaster _boardRaycaster; // Canvas Lobby의 Raycaster (포커스 중에만 켬)
    [SerializeField] private GameObject _crosshair;
    [SerializeField] private float _moveSeconds = 0.35f;
    [SerializeField] private string _prompt = "게시판 보기";

    private PlayerInputReader _input;
    private Transform _camera;
    private Coroutine _move;
    private PlayerInteractor _interactor;


    // 지금 포커스 중인 게시판. ESC 처리는 GameMenuUI가 이 값을 보고 결정
    public static LobbyBoardFocus Current { get; private set; }
    public bool IsFocused { get; private set; }
    public string InteractionPrompt => _prompt;

    private void Start()
    {
        if (_boardRaycaster != null)
        {
            _boardRaycaster.enabled = false;
        }
    }

    public bool CanInteract(GameObject interactor) => !IsFocused && _move == null && _focusPoint != null && !GameMenuUI.IsOpen;

    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;

        _input = interactor.GetComponent<PlayerInputReader>();
        _camera = interactor.GetComponentInChildren<Camera>().transform;
        _interactor = interactor.GetComponent<PlayerInteractor>();
        if (_interactor != null) _interactor.CanInteract = false;

        IsFocused = true;
        Current = this;
        _input.SetGameplayInputBlocked(true);
        SetBoardMode(true);
        _move = StartCoroutine(MoveCamera(true));
    }

    // ESC(Cancel), [돌아가기] 버튼
    public void Unfocus()
    {
        if (!IsFocused) return;

        IsFocused = false;
        if (Current == this) Current = null;
        SetBoardMode(false);
        if (_move != null) StopCoroutine(_move);
        _move = StartCoroutine(MoveCamera(false));
    }

    private void SetBoardMode(bool on)
    {
        if (_boardRaycaster != null) _boardRaycaster.enabled = on;
        if (_crosshair != null) _crosshair.SetActive(!on);
        Cursor.visible = on;
        Cursor.lockState = on ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private IEnumerator MoveCamera(bool toBoard)
    {
        Vector3 startPos = _camera.position;
        Quaternion startRot = _camera.rotation;

        for (float t = 0f; t < 1f; t += Time.deltaTime / _moveSeconds)
        {
            float k = Mathf.SmoothStep(0f, 1f, t);
            GetTarget(toBoard, out Vector3 pos, out Quaternion rot);
            _camera.SetPositionAndRotation(Vector3.Lerp(startPos, pos, k), Quaternion.Slerp(startRot, rot, k));
            yield return null;
        }

        GetTarget(toBoard, out Vector3 endPos, out Quaternion endRot);
        _camera.SetPositionAndRotation(endPos, endRot);

        // 복귀가 끝난 뒤에 이동·시점을 풀어 줌 (돌아오는 도중에 움직이지 않도록)
        if (!toBoard)
        {
            _input.SetGameplayInputBlocked(false);
            if (_interactor != null) _interactor.CanInteract = true;
        }
        _move = null;
    }

    // 게시판: FocusPoint / 복귀: 카메라 원래 자리 = CameraPivot 위치 (Main Camera 로컬 0,0,0)
    private void GetTarget(bool toBoard, out Vector3 pos, out Quaternion rot)
    {
        Transform target = toBoard ? _focusPoint : _camera.parent;
        pos = target.position;
        rot = target.rotation;
    }

    private void OnDestroy()
    {
        if (Current == this) Current = null;
    }
}
