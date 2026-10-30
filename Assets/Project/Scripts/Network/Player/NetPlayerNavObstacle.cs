using UnityEngine;
using UnityEngine.AI;

// 플레이어 NavMeshObstacle을 서 있을 때만 켠다
// 서 있음: 걸어오던 주민이 플레이어를 비켜 돌아간다
// 움직임: 꺼 둔다. 켜 두면 주민이 회피 계산에 밀려 쭉쭉 밀려난다. 걷다 부딪히면 주민 어깨빵(AI_Bump)으로 처리한다
// 주민 길찾기는 서버에서만 돌지만, 판정이 가벼워 모든 피어에서 같은 규칙으로 돈다
[RequireComponent(typeof(NavMeshObstacle))]
public class NetPlayerNavObstacle : MonoBehaviour
{
    [Tooltip("이 속도(m/s) 아래면 서 있는 것으로 본다")]
    [SerializeField, Min(0f)] private float _stillSpeed = 0.3f;
    [Tooltip("이 시간 동안 서 있어야 장애물을 켠다 (잠깐 멈춘 것엔 반응하지 않음)")]
    [SerializeField, Min(0f)] private float _stillSeconds = 0.3f;

    private NavMeshObstacle _obstacle;
    private Vector3 _lastPosition;
    private float _stillTime;

    private void Awake()
    {
        _obstacle = GetComponent<NavMeshObstacle>();
        _lastPosition = transform.position;
        _obstacle.enabled = false;
    }

    private void Update()
    {
        Vector3 position = transform.position;
        Vector3 moved = position - _lastPosition;
        moved.y = 0f;
        _lastPosition = position;

        bool still = moved.magnitude <= _stillSpeed * Mathf.Max(Time.deltaTime, 0.0001f);
        _stillTime = still ? _stillTime + Time.deltaTime : 0f;

        bool enable = _stillTime >= _stillSeconds;
        if (_obstacle.enabled != enable)
        {
            _obstacle.enabled = enable;
        }
    }
}
