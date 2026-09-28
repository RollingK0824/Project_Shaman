using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.World
{
    public class PlaceArea : MonoBehaviour
    {
        private const float NAVMESH_SAMPLE_DISTANCE = 3f;
        private const int RANDOM_POINT_ATTEMPTS = 8;

        [SerializeField] private string _placeId;
        [SerializeField] private string _displayName;
        [SerializeField] private PlaceType _placeType = PlaceType.Work;
        [SerializeField] private PlaceShape _shape = PlaceShape.Sphere;
        [SerializeField] private float _radius = 2f;
        [SerializeField] private Vector3 _boxSize = new Vector3(4f, 2f, 4f);
        [SerializeField] private int _maxPeople = 1;

        public string PlaceId => _placeId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _placeId : _displayName;
        public PlaceType Type => _placeType;
        public int MaxPeople => _maxPeople;
        public Vector3 Center => transform.position;

        private void OnEnable()
        {
            PlaceRegistry.Register(this);
        }

        private void OnDisable()
        {
            PlaceRegistry.Unregister(this);
        }

        public Vector3 GetRandomPoint(System.Random random)
        {
            for (int i = 0; i < RANDOM_POINT_ATTEMPTS; i++)
            {
                Vector3 candidate = Center + GetRandomOffset(random);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return Center;
        }

        public bool Contains(Vector3 position)
        {
            Vector3 local = position - Center;

            if (_shape == PlaceShape.Sphere)
            {
                local.y = 0f;
                return local.sqrMagnitude <= _radius * _radius;
            }

            Vector3 half = _boxSize * 0.5f;
            Vector3 rotated = Quaternion.Inverse(transform.rotation) * local;
            return Mathf.Abs(rotated.x) <= half.x && Mathf.Abs(rotated.z) <= half.z;
        }

        private Vector3 GetRandomOffset(System.Random random)
        {
            float u = (float)random.NextDouble();
            float v = (float)random.NextDouble();

            if (_shape == PlaceShape.Sphere)
            {
                float angle = u * Mathf.PI * 2f;
                float distance = Mathf.Sqrt(v) * _radius;
                return new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            }

            Vector3 half = _boxSize * 0.5f;
            Vector3 local = new Vector3(Mathf.Lerp(-half.x, half.x, u), 0f, Mathf.Lerp(-half.z, half.z, v));
            return transform.rotation * local;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = GetGizmoColor();

            if (_shape == PlaceShape.Sphere)
            {
                Gizmos.DrawWireSphere(Center, _radius);
            }
            else
            {
                Gizmos.matrix = Matrix4x4.TRS(Center, transform.rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, _boxSize);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }

        private Color GetGizmoColor()
        {
            switch (_placeType)
            {
                case PlaceType.Work:
                    return new Color(1f, 0.6f, 0.1f);
                case PlaceType.Rest:
                    return new Color(0.2f, 0.8f, 0.3f);
                case PlaceType.Home:
                    return new Color(0.3f, 0.5f, 1f);
                default:
                    return Color.gray;
            }
        }
    }
}
