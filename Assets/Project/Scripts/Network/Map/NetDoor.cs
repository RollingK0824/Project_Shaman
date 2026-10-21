using UnityEngine;
using Mirror;
using Steamworks;
using System.Runtime.CompilerServices;

//  씬에 배치된 문짝 하나. 열림 상태는 서버가 정하고 SyncVar로 모든 클라에 동기화
[RequireComponent(typeof(NetworkIdentity), typeof(MeshFilter))]
public class NetDoor : NetworkBehaviour, IInteractable
{
    public enum DoorMode { Swing, Slide}

    [Header("Motion")]
    [SerializeField] private DoorMode _mode = DoorMode.Swing;
    [Tooltip("경첩 (미닫이는 밀리는 방향)을 메시 반대쪽 끝으로")]
    [SerializeField] private bool _hingeAtMax;
    [SerializeField] private float _openAngle = 100f;
    [SerializeField] private float _speed = 3f;

    [Header("Server Rule")]
    [SerializeField] private float _maxUseDistance = 4f;
    [SerializeField] private float _cooldown = 0.4f;

    // [서버 -> 모든 클라] 0 = 닫힘, +1 / -1 : 열린 방향
    [SyncVar] private int _openSign;

    private Vector3 _closedPos;
    private Quaternion _closedRot;
    private Vector3 _hingeWorld;
    private Vector3 _centerWorld;
    private Vector3 _slideWorld;
    private float _progress;
    private double _nextUseTime;

    public bool IsOpen => _openSign != 0;
    public string InteractionPrompt => IsOpen ? "문 닫기" : "문 열기";


    private void Awake()
    {
        _closedPos = transform.position;
        _closedRot = transform.rotation;

        // 피벗이 경첩이라는 보장이 없기에 메시 바운드의 폭 방향 끝을 경침으로사용
        Bounds b = GetComponent<MeshFilter>().sharedMesh.bounds;
        Vector3 half = b.size.x >= b.size.z ? new Vector3(b.extents.x, 0f, 0f) : new Vector3(0f, 0f, b.extents.z);
        if (!_hingeAtMax) half = -half;

        _centerWorld = transform.TransformPoint(b.center);
        _hingeWorld = transform.TransformPoint(b.center + half);
        _slideWorld = transform.TransformVector(half * 2f);
    }

    // [클라] 늦게 들어온 클라는 애니메이션 없이 현재 상태로 바로 맞춤
    public override void OnStartClient()
    {
        base.OnStartClient();

        _progress = _openSign;
        ApplyPose();
    }

    private void Update()
    {
        if (Mathf.Approximately(_progress, _openSign)) return;

        _progress = Mathf.MoveTowards(_progress, _openSign, _speed * Time.deltaTime);
        ApplyPose();
    }

    private void ApplyPose()
    {
        if (_mode == DoorMode.Slide)
        {
            transform.SetPositionAndRotation(_closedPos + _slideWorld * Mathf.Abs(_progress), _closedRot);
            return;
        }

        Quaternion swing = Quaternion.AngleAxis(_openAngle * _progress, _closedRot * Vector3.up);
        transform.SetPositionAndRotation(_hingeWorld + swing * (_closedPos - _hingeWorld), swing * _closedRot);
    }

    public bool CanInteract(GameObject interactor)
    {
        return isClient && PlayerActionGuard.CanAct(interactor);
    }

    // [로컬 클라] PlayerInteractor가 F 입력 때 호출 -> 서버에 토글 요청만 보냄
    public void Interact(GameObject interactor)
    {
        if (!CanInteract(interactor)) return;
        if (!interactor.TryGetComponent(out NetworkIdentity identity) || !identity.isLocalPlayer) return;

        CmdToggle();
    }


    // [서버] 문은 플레이어 소유가 아니기에 requiresAuthority = false, 요청자는 sender로 확인

    [Command(requiresAuthority = false)]
    private void CmdToggle(NetworkConnectionToClient sender = null)
    {
        GameObject player = sender != null && sender.identity != null ? sender.identity.gameObject : null;
        if (player == null || !PlayerActionGuard.CanAct(player)) return;
        if (NetworkTime.time < _nextUseTime) return;
        if (Vector3.Distance(player.transform.position, _centerWorld) > _maxUseDistance) return;

        _nextUseTime = NetworkTime.time + _cooldown;

        if (IsOpen)
        {
            _openSign = 0;
            return;
        }

        // +각도로 돌 때 문 중심이 움직이는 방향이 플레이어 쪽이면 반대로 열기
        Vector3 swingDir = Vector3.Cross(_closedRot * Vector3.up, _centerWorld - _hingeWorld);
        Vector3 toPlayer = player.transform.position - _centerWorld;
        _openSign = Vector3.Dot(swingDir, toPlayer) > 0f ? -1 : 1;
    }
}
