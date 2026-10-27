using UnityEngine;
using Mirror;
using ProjectShaman.AI;


// 음기향 숙주 방향 온라인 연결
// [서버]         음기형을 듬 플레이어만 0.25초마다 숙주 방향을 샘플링해서 SyncVar로 공개
// [모든 클라]     받은 방향,near를 그 플레이어의 PlayerYinIncenseSense에 넣어 1,3인칭 연기에 반영
// [호스트]        PlayerYinIncenseSense.Read가 직접 샘플링하므로 넣지 않음
[RequireComponent(typeof(NetworkIdentity), typeof(NetPlayerEquipment))]
public class NetPlayerYinIncense : NetworkBehaviour
{
    private const string IncenseItemId = "item_incense";
    private const float SampleInterval = .25f;

    // [서버 -> 모든 클라] 수평 방향 (x, z), 숙주 1.5m 이내 여부
    [SyncVar] private Vector2 _direction;
    [SyncVar] private bool _near;

    private NetPlayerEquipment _equipment;
    private NetPlayerStatus _status;
    private PlayerYinIncenseSense _sense;
    private float _nextSample;

    private void Awake()
    {
        _equipment = GetComponent<NetPlayerEquipment>();
        _status = GetComponent<NetPlayerStatus>();
    }

    private PlayerYinIncenseSense Sense
    {
        get
        {
            if (_sense == null) _sense = GetComponent<PlayerYinIncenseSense>();
            return _sense;
        }
    }

    private void Update()
    {
        if (isServer)
        {
            ServerSample();
            return;
        }

        if (isClient && Sense != null)
        {
            Sense.ApplyAuthoritativeSense(new Vector3(_direction.x, 0f, _direction.y), _near);
        }
    }

    [Server]
    private void ServerSample()
    {
        if (Time.unscaledTime < _nextSample) return;
        _nextSample = Time.unscaledTime + SampleInterval;


        // 클라가 보낸 값이 아니라 서버가 확정한 장착 아이템 기준
        ItemData equipped = _equipment.EquippedItem;
        bool holding = equipped != null && equipped.ItemId == IncenseItemId && (_status == null || !_status.IsDead);

        Vector3 direction = Vector3.zero;
        bool near = false;
        if (holding && Sense != null) Sense.TrySampleAuthoritative(out direction, out near);

        // 값이 같으면 SyncVar가 더티 표시되지 않아 전송도 없음
        _direction = new Vector2(direction.x, direction.z);
        _near = near;
    }

}
