using UnityEngine;

[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerCameraController))]
[RequireComponent(typeof(PlayerInteractor))]
public class PlayerInspectController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform _inspectAnchor;

    [Header("Rotation")]
    [SerializeField] private float _rotationSensitivity = 0.2f;

    public bool IsInspecting { get; private set; }

    private PlayerInputReader _inputReader;
    private PlayerController _playerController;
    private PlayerCameraController _cameraController;
    private PlayerInteractor _playerInteractor;

    private InspectInteractable _currentTarget;
    private Transform _targetTransform;

    private Transform _originalParent;
    private Vector3 _originalLocalPosition;
    private Quaternion _originalLocalRotation;
    private Vector3 _originalLocalScale;

    private Collider[] _targetColliders;
    private bool[] _colliderStates;

    private Rigidbody _targetRigidbody;
    private bool _originalIsKinematic;
    private bool _originalUseGravity;

    private void Awake()
    {
        _inputReader = GetComponent<PlayerInputReader>();
        _playerController = GetComponent<PlayerController>();
        _cameraController = GetComponent<PlayerCameraController>();
        _playerInteractor = GetComponent<PlayerInteractor>();
    }

    private void Update()
    {
        if (!IsInspecting ||
            _targetTransform == null)
        {
            return;
        }

        RotateTarget();
    }

    private void OnDisable()
    {
        if (IsInspecting)
        {
            EndInspection();
        }
    }

    public void BeginInspection(InspectInteractable target)
    {
        if (IsInspecting ||
            target == null ||
            _inspectAnchor == null)
        {
            return;
        }

        _currentTarget = target;
        _targetTransform = target.transform;

        SaveOriginalTransform();
        SaveAndDisablePhysics();

        // 상세 조사 중 플레이어 조작 제한
        _playerController.CanMove = false;
        _cameraController.CanLook = false;
        _playerInteractor.CanInteract = false;

        // 조사 대상 오브젝트를 카메라 앞 Anchor로 이동
        _targetTransform.SetParent(
            _inspectAnchor,
            false
        );

        _targetTransform.localPosition =
            target.InspectLocalPosition;

        _targetTransform.localRotation =
            Quaternion.Euler(
                target.InspectLocalRotation
            );

        _targetTransform.localScale =
            target.InspectLocalScale;

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

        RestoreOriginalTransform();
        RestorePhysics();

        _playerController.CanMove = true;
        _cameraController.CanLook = true;
        _playerInteractor.CanInteract = true;

        if (_currentTarget != null)
        {
            Debug.Log(
                $"[Inspect] 상세 조사 종료: " +
                $"{_currentTarget.gameObject.name}",
                _currentTarget.gameObject
            );
        }

        _currentTarget = null;
        _targetTransform = null;

        IsInspecting = false;
    }

    private void RotateTarget()
    {
        Vector2 lookInput =
            _inputReader.LookInput;

        float yaw =
            -lookInput.x *
            _rotationSensitivity;

        float pitch =
            lookInput.y *
            _rotationSensitivity;

        _targetTransform.Rotate(
            Vector3.up,
            yaw,
            Space.World
        );

        _targetTransform.Rotate(
            Vector3.right,
            pitch,
            Space.Self
        );
    }

    private void SaveOriginalTransform()
    {
        _originalParent =
            _targetTransform.parent;

        _originalLocalPosition =
            _targetTransform.localPosition;

        _originalLocalRotation =
            _targetTransform.localRotation;

        _originalLocalScale =
            _targetTransform.localScale;
    }

    private void RestoreOriginalTransform()
    {
        if (_targetTransform == null)
        {
            return;
        }

        _targetTransform.SetParent(
            _originalParent,
            false
        );

        _targetTransform.localPosition =
            _originalLocalPosition;

        _targetTransform.localRotation =
            _originalLocalRotation;

        _targetTransform.localScale =
            _originalLocalScale;
    }

    private void SaveAndDisablePhysics()
    {
        _targetColliders =
            _targetTransform
                .GetComponentsInChildren<Collider>(true);

        _colliderStates =
            new bool[_targetColliders.Length];

        for (int i = 0;
             i < _targetColliders.Length;
             i++)
        {
            _colliderStates[i] =
                _targetColliders[i].enabled;

            _targetColliders[i].enabled = false;
        }

        _targetRigidbody =
            _targetTransform.GetComponent<Rigidbody>();

        if (_targetRigidbody == null)
        {
            return;
        }

        _originalIsKinematic =
            _targetRigidbody.isKinematic;

        _originalUseGravity =
            _targetRigidbody.useGravity;

        _targetRigidbody.isKinematic = true;
        _targetRigidbody.useGravity = false;
    }

    private void RestorePhysics()
    {
        if (_targetColliders != null &&
            _colliderStates != null)
        {
            int count = Mathf.Min(
                _targetColliders.Length,
                _colliderStates.Length
            );

            for (int i = 0;
                 i < count;
                 i++)
            {
                if (_targetColliders[i] != null)
                {
                    _targetColliders[i].enabled =
                        _colliderStates[i];
                }
            }
        }

        if (_targetRigidbody != null)
        {
            _targetRigidbody.isKinematic =
                _originalIsKinematic;

            _targetRigidbody.useGravity =
                _originalUseGravity;
        }

        _targetColliders = null;
        _colliderStates = null;
        _targetRigidbody = null;
    }
}