using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using UnityEngine.UI;


[ExecuteAlways]
[RequireComponent(typeof(SplineContainer), typeof(MeshFilter), typeof(MeshRenderer))]
public class SplineRoad : MonoBehaviour
{
    [SerializeField] private float _width = 3f;
    [SerializeField] private float _segmentLength = 0.5f; // 몇 m마다 생성할지
    [SerializeField] private float _uvTileLength = 3f;    // 텍스처 한 장이 덮는 길이

    [Header("지면 붙이기")]
    [SerializeField] private bool _snapToGround = true;
    [SerializeField] private LayerMask _groundMask = ~0; // Road 레이어는 반드시 제외
    [SerializeField] private float _heightOffset = 0.03f; // Terrain과 겹쳐 깜빡이는거 방지

    private SplineContainer _container;
    private Mesh _mesh;
    private MeshRenderer _renderer;

    private void OnEnable()
    {
        _container = GetComponent<SplineContainer>();
        Spline.Changed += OnSplineChanged;
        Rebuild();
        UpdateVisibility();
    }

    private void OnDisable()
    {
        Spline.Changed -= OnSplineChanged;
    }

    void OnSplineChanged(Spline spline, int kontIndex, SplineModification modification)
    {
        if (_container != null && spline == _container.Spline)
            Rebuild();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Rebuild(); };
    }
    private void Update()
    {
        // 편집 모드에서 머티리얼을 넣고 빼면 바로 반영
        if (!Application.isPlaying) UpdateVisibility();
    }
#endif

    [ContextMenu("Rebuild")]
    public void Rebuild()
    {
        if (Application.isPlaying) return;
        if (_container == null) _container = GetComponent<SplineContainer>();

        Spline spline = _container.Spline;

        if (spline == null || spline.Count < 2) return;

        int segments = Mathf.Max(1, Mathf.CeilToInt(spline.GetLength() / _segmentLength));
        int rings = segments + 1;
        var vertices = new Vector3[rings * 2];
        var uvs = new Vector2[rings * 2];
        var triangles = new int[segments * 6];

        float3 right = new float3(1f, 0f, 0f);
        float3 prevPos = default;
        float distance = 0f;

        for (int i = 0; i < rings; i++)
        {
            float t = (float)i / segments;
            spline.Evaluate(t, out float3 pos, out float3 tangent, out float3 up);

            if (math.lengthsq(tangent) > 1e-6f)
            {
                right = math.normalize(math.cross(up, tangent));
            }

            if (i > 0)
                distance += math.distance(prevPos, pos);
            prevPos = pos;

            float3 half = right * (_width * 0.5f);
            vertices[i * 2] = Snap(pos - half);
            vertices[i * 2 + 1] = Snap(pos + half);

            float v = distance / _uvTileLength;
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        for (int i = 0; i < segments; i++)
        {
            int l0 = i * 2, r0 = l0 + 1, l1 = l0 + 2, r1 = l0 + 3;
            int k = i * 6;
            triangles[k] = l0; triangles[k + 1] = l1; triangles[k + 2] = r0;
            triangles[k + 3] = l1; triangles[k + 4] = r1; triangles[k + 5] = r0;
        }

        if (_mesh == null) _mesh = new Mesh { name = "SplineRoad" };
        _mesh.Clear();
        _mesh.vertices = vertices;
        _mesh.uv = uvs;
        _mesh.triangles = triangles;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        GetComponent<MeshFilter>().sharedMesh = _mesh;
        if (TryGetComponent(out MeshCollider col))
        {
            col.sharedMesh = null; // null을 거쳐야 콜라이더가 새 메시로 갱신된다
            col.sharedMesh = _mesh;
        }

    }

    // 머티리얼이 없으면 렌더러를 꺼서 분홍색 대신 안 보이게 한다 (충돌/NavMesh는 유지)
    private void UpdateVisibility()
    {
        if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
        _renderer.enabled = _renderer.sharedMaterial != null;
    }

    Vector3 Snap(float3 localPoint)
    {
        Vector3 world = transform.TransformPoint(localPoint);
        if (_snapToGround &&
            Physics.Raycast(world + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f, _groundMask))
        {
            world.y = hit.point.y + _heightOffset;
        }
        return transform.InverseTransformPoint(world);
    }

}
