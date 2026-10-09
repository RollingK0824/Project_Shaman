using UnityEngine;

public class PlayerSpawnArea : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _radius = 2f;

    [Header("지면 맞춤")]
    [SerializeField] private float _groundCheckHeight = 10f;
    [SerializeField] private float _heightOffset = 0.1f;
    [SerializeField] private LayerMask _groundMask = ~0;

    [Header("에디터 미리보기")]
    [SerializeField, Min(1)] private int _previewSlots = 4;

    // index번 플레이어(0부터)의 위치와 회전. slotCount는 방의 최대 인원.
    public void GetSpawnPose(int index, int slotCount, out Vector3 position, out Quaternion rotation)
    {
        Vector3 center = transform.position;
        position = GetCirclePoint(index, slotCount);

        // 지형이 평평하지 않아도 지면 위에 서도록 위에서 아래로 확인
        Vector3 rayStart = position + Vector3.up * _groundCheckHeight;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, _groundCheckHeight * 2f, _groundMask, QueryTriggerInteraction.Ignore))
        {
            position = hit.point;
        }
        position += Vector3.up * _heightOffset;

        // 모두 중심을 바라보게
        Vector3 toCenter = center - position;
        toCenter.y = 0f;
        rotation = toCenter.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toCenter) : transform.rotation;
    }

    private Vector3 GetCirclePoint(int index, int slotCount)
    {
        slotCount = Mathf.Max(1, slotCount);
        float angle = 360f * (index % slotCount) / slotCount;
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (transform.forward * _radius);
        return transform.position + offset;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        for (int i = 0; i < _previewSlots; i++)
        {
            Vector3 point = GetCirclePoint(i, _previewSlots);
            Gizmos.DrawWireSphere(point, 0.4f);
            Gizmos.DrawLine(transform.position, point);
        }
    }
}
