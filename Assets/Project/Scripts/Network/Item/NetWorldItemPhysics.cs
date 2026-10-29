using UnityEngine;
using Mirror;

// [서버] 버린 아이템을 물리로 바닥까지 떨어뜨리고, 멈추면 다시 고정
// [클라] 위치는 NetworkRigidbodyUnreliable이 서버 값으로 맞춰 줌 (클라에서는 항상 kinematic)
// 씬에 배치된 아이템, 벽에 건 초롱불은 ServerBeginDrop을 부르지 않으므로 그대로 고정
[RequireComponent(typeof(Rigidbody))]
public class NetWorldItemPhysics : NetworkBehaviour
{
    [SerializeField, Min(0f)] private float _dropHeight = .6f;        // 손 높이쯤에서 떨어지는 느낌
    [SerializeField, Min(.5f)] private float _maxFallTime = 4f;       // 이 시간이 지나면 강제로 멈춤
    [SerializeField, Min(1f)] private float _maxFallDistance = 20f;   // 이만큼 떨어지면 바닥을 뚫은 것으로 보고 처음 자리로
    [SerializeField, Min(.01f)] private float _restSpeed = .05f;      // 이 속도 아래면 멈춘 것으로 봄

    private Rigidbody _rb;
    private Vector3 _startPosition;
    private float _fallStartTime;
    private bool _falling;

    private void Awake() => _rb = GetComponent<Rigidbody>();

    // [서버] NetPlayerItemDrop이 Spawn 직전에 호출. 들어 올린 위치가 Spawn 메시지로 클라에 그대로 전달됨
    [Server]
    public void ServerBeginDrop()
    {
        _startPosition = transform.position;

        // 위가 비어 있을 때만 들어 올림 (낮은 천장 밑이면 그 자리에서 시작)
        if (_dropHeight > 0f && !Physics.Raycast(transform.position, Vector3.up, _dropHeight + .1f, ~0, QueryTriggerInteraction.Ignore))
            transform.position += Vector3.up * _dropHeight;

        _rb.isKinematic = false;
        _rb.useGravity = true;
        _fallStartTime = Time.time;
        _falling = true;
    }

    [ServerCallback]
    private void FixedUpdate()
    {
        if (!_falling) return;

        // 바닥을 뚫고 떨어졌으면 드롭 컨트롤러가 처음 정한 자리로 되돌림
        if (_startPosition.y - transform.position.y > _maxFallDistance)
        {
            Stop();
            transform.position = _startPosition;
            return;
        }

        float elapsed = Time.time - _fallStartTime;
        bool resting = elapsed > .2f && _rb.linearVelocity.sqrMagnitude < _restSpeed * _restSpeed;
        if (resting || _rb.IsSleeping() || elapsed > _maxFallTime) Stop();
    }

    [Server]
    private void Stop()
    {
        _falling = false;
        _rb.linearVelocity = Vector3.zero;
        _rb.isKinematic = true;
    }
}