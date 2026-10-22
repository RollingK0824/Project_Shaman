using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectShaman.Map
{
    public enum MapAreaShape
    {
        Box,
        Sphere
    }

    // A named region placed in the scene (house, yard, market, road ...). Nest areas as child GameObjects:
    // the deepest one containing a position wins, so "시장터 > 1번 상가" resolves to "1번 상가".
    [DisallowMultipleComponent]
    public class MapArea : MonoBehaviour, IMapArea
    {
        [SerializeField] private string _areaId;
        [SerializeField] private string _displayName;
        [Tooltip("Optional. Prefixes the name as \"{owner}네 {name}\" (e.g. 김씨네 집)")]
        [SerializeField] private string _ownerName;
        [SerializeField] private MapAreaShape _shape = MapAreaShape.Box;
        [SerializeField] private Vector3 _boxSize = new Vector3(10f, 4f, 10f);
        [SerializeField] private float _radius = 5f;
        [SerializeField] private Color _gizmoColor = new Color(0.3f, 0.8f, 1f, 1f);

        private int _depth = -1;

        public string AreaId => _areaId;
        public string DisplayName
        {
            get
            {
                string name = string.IsNullOrEmpty(_displayName) ? _areaId : _displayName;
                return string.IsNullOrEmpty(_ownerName) ? name : $"{_ownerName}네 {name}";
            }
        }

        public Vector3 Center => transform.position;
        public float Footprint => _shape == MapAreaShape.Sphere ? Mathf.PI * _radius * _radius : Mathf.Abs(_boxSize.x * _boxSize.z);

        public int Depth
        {
            get
            {
                if (_depth < 0)
                {
                    _depth = CountParents(transform);
                }

                return _depth;
            }
        }

        // Runtime owner assignment (house given to a villager). Scene-only data, so peers that assign the same owner agree.
        public void SetOwnerName(string ownerName)
        {
            _ownerName = ownerName;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 local = position - Center;

            if (_shape == MapAreaShape.Sphere)
            {
                local.y = 0f;
                return local.sqrMagnitude <= _radius * _radius;
            }

            Vector3 rotated = Quaternion.Inverse(transform.rotation) * local;
            return Mathf.Abs(rotated.x) <= _boxSize.x * 0.5f && Mathf.Abs(rotated.z) <= _boxSize.z * 0.5f;
        }

        private void OnEnable()
        {
            _depth = -1;
            MapAreaRegistry.Register(this);
        }

        private void OnDisable()
        {
            MapAreaRegistry.Unregister(this);
        }

        private void OnTransformParentChanged()
        {
            _depth = -1;
        }

        // Areas and places both count as levels, so a house inside a work place nests correctly
        internal static int CountParents(Transform transform)
        {
            int depth = 0;

            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                if (parent.GetComponent<IMapArea>() != null)
                {
                    depth++;
                }
            }

            return depth;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_areaId))
            {
                Debug.LogWarning($"[MapArea] '{name}' has no AreaId", this);
                return;
            }

            foreach (MapArea other in FindObjectsByType<MapArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other != this && other.gameObject.scene == gameObject.scene && other._areaId == _areaId)
                {
                    Debug.LogWarning($"[MapArea] Duplicate AreaId '{_areaId}' on '{name}' and '{other.name}'", this);
                }
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.6f);
            Matrix4x4 previous = Gizmos.matrix;

            if (_shape == MapAreaShape.Sphere)
            {
                Handles.color = Gizmos.color;
                Handles.DrawWireDisc(Center, Vector3.up, _radius);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(Center, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(_boxSize.x, 0.05f, _boxSize.z));
                Gizmos.matrix = previous;
            }

            GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = _gizmoColor } };
            Handles.Label(Center + Vector3.up * 1.5f, $"{DisplayName}\n<{_areaId}>", style);
        }
#endif
    }
}
