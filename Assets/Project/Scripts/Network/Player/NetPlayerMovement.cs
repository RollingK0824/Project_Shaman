using UnityEngine;
using Mirror;
using System.Runtime.CompilerServices;

[RequireComponent(typeof(NetworkIdentity))]
public class NetPlayerMovement : NetworkBehaviour
{
    private PlayerMoveProposal _pendingProposal;    // 서버가 검사할 이동 요청 보관
    private bool _hasPendingProposal;               // 처리할 요청이 들어왔는지 표시

    [SerializeField, Min(0.01f)] private float _sendInterval = 0.05f;

    private double _nextSendTime;
    private uint _sequence;
    private uint _revision;


    private uint _lastReceivedSequence;     // 마지막으로 수신 순서 검사를 통과한 요청의 순번
    private uint _serverRevision;           // 서버의 현재 보정 버전
    private bool _hasReceivedSequence;      // 순번 비교의 기준이 되는 요청을 한 번이라도 받았는지

    private Vector3 _serverPosition;
    private Quaternion _serverRotation;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _lastReceivedSequence = 0;
        _serverRevision = 0;
        _hasReceivedSequence = false;
        _hasPendingProposal = false;

        _serverPosition = transform.position;
        _serverRotation = transform.rotation;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        _nextSendTime = 0;
        _sequence = 0;
        _revision = 0;
    }

    private void LateUpdate()
    { 
        if (!isLocalPlayer)
        {
            return;
        }
        double now = NetworkTime.localTime;


        if (now < _nextSendTime)
            return;

        _nextSendTime = now + _sendInterval;

        PlayerMoveProposal proposal = new PlayerMoveProposal
        {
            sequence = _sequence,
            revision = _revision,
            position = transform.position,
            yaw = transform.eulerAngles.y
        };

        CmdSubmitMove(proposal);
    }

    // 소유 클라이언트가 호출하면 서버에서 실행되는 수신 함수
    [Command(channel = Channels.Unreliable)] // 이전 위치의 재전송을 기다리지 않고 새로운 이동 결과를 보내기 위한 채널
    private void CmdSubmitMove(PlayerMoveProposal proposal)
    {
        if (proposal.revision != _serverRevision)
        {
            return;
        }

        if (_hasPendingProposal && !IsNewerSequence(proposal.sequence, _lastReceivedSequence))
        {
            return;
        }

        _lastReceivedSequence = proposal.sequence;
        _hasPendingProposal = true;

        _pendingProposal = proposal;
        _hasPendingProposal = true;
    }
    
    private static bool IsNewerSequence(uint incoming, uint previous)
    {
        return unchecked((int)incoming - previous) > 0;
    }

}


