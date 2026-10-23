using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI
{
    // Free flight in the manner of a vex: steered 3D velocity (curves, no instant turns), each flyer on its own
    // cruise height, weaving and coming round to its destination from its own bearing, so a swarm pours in from
    // every side. Walls are flown through; the terrain is not. Near the destination it drops to the target's height.
    public class AI_FlyingMovement : MonoBehaviour, INetworkMovable
    {
        private static readonly List<AI_FlyingMovement> _active = new List<AI_FlyingMovement>();

        [SerializeField, Min(0.1f)] private float _defaultSpeed = 3f;
        [SerializeField, Min(0f)] private float _arriveDistance = 0.5f;
        [Tooltip("How fast the velocity can change (m/s²): lower = wider, lazier curves")]
        [SerializeField, Min(0.1f)] private float _acceleration = 10f;
        [Tooltip("Yaw only, toward where the flyer is heading (the weave does not turn the body)")]
        [SerializeField, Min(0f)] private float _turnSpeed = 180f;

        [Header("Height")]
        [Tooltip("Height above the ground while far from the destination, picked per flyer in this range")]
        [SerializeField] private Vector2 _cruiseHeightRange = new Vector2(4f, 8f);
        [Tooltip("Within this flat distance of the destination the flyer comes down to it")]
        [SerializeField, Min(0.1f)] private float _diveDistance = 8f;
        [Tooltip("Height above the destination point once there")]
        [SerializeField] private float _hoverHeight = 0.8f;
        [SerializeField, Min(0f)] private float _groundClearance = 0.5f;
        [Tooltip("Never higher than this above the ground (or above the destination where there is no terrain)")]
        [SerializeField, Min(0.5f)] private float _maxHeight = 10f;
        [SerializeField, Min(0f)] private float _heightFollowRate = 3f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.15f;
        [SerializeField, Min(0f)] private float _bobFrequency = 1.5f;

        [Header("Weave")]
        [Tooltip("Sideways / up-down sway while flying (m)")]
        [SerializeField, Min(0f)] private float _weaveAmplitude = 1.5f;
        [SerializeField, Min(0f)] private float _weaveFrequency = 0.35f;
        [Tooltip("Far from the destination the flyer first heads for a point this far out on its own bearing, then turns in")]
        [SerializeField, Min(0f)] private float _approachRadius = 8f;

        [Header("Separation")]
        [SerializeField, Min(0f)] private float _separationRadius = 1.2f;
        [SerializeField, Min(0f)] private float _separationStrength = 1.5f;

        [Header("Runtime")]
        [SerializeField] private bool _isActive;
        [SerializeField] private bool _isStopped = true;
        [SerializeField] private Vector3 _destination;
        [SerializeField] private float _speed;

        private Vector3 _velocity;
        private float _cruiseHeight;
        private float _bobPhase;
        private float _weaveSeed;
        private Vector3 _approachBearing;
        private Vector3 _heading;
        private float _fallbackGround;
        private bool _hasDestination;

        public bool IsStopped => _isStopped;
        public bool HasReachedDestination => _hasDestination && FlatDistance(transform.position, _destination) <= _arriveDistance;
        public float CurrentSpeed => _velocity.magnitude;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _active.Clear();
        }

        private void Awake()
        {
            _speed = _defaultSpeed;
            _bobPhase = Random.Range(0f, Mathf.PI * 2f);
            _weaveSeed = Random.Range(0f, 1000f);
            _cruiseHeight = Random.Range(Mathf.Min(_cruiseHeightRange.x, _cruiseHeightRange.y), Mathf.Max(_cruiseHeightRange.x, _cruiseHeightRange.y));
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _approachBearing = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        public void SetAgentActive(bool active)
        {
            _isActive = active;

            if (active)
            {
                if (!_active.Contains(this))
                {
                    _active.Add(this);
                }

                _fallbackGround = transform.position.y - _hoverHeight;
            }
            else
            {
                _active.Remove(this);
                Stop();
                _velocity = Vector3.zero;
            }
        }

        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0.1f, speed);
        }

        public void MoveTo(Vector3 destination)
        {
            _destination = destination;
            _hasDestination = true;
            _isStopped = false;
        }

        // Brakes rather than freezing: the flyer drifts to a hover
        public void Stop()
        {
            _isStopped = true;
        }

        private void Update()
        {
            if (!_isActive)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            Vector3 position = transform.position;
            // Off the terrain the ground is unknown: the destination's height (or where it spawned) stands in,
            // never the flyer's own height, which would let it climb without end
            float ground = GroundHeight(position, _hasDestination ? _destination.y : _fallbackGround);
            bool moving = !_isStopped && _hasDestination;

            Vector3 desired = moving ? FlightVelocity(position, ground) : HoverVelocity(position, ground);
            desired += Separation(position) * _separationStrength;

            _velocity = Vector3.MoveTowards(_velocity, desired, _acceleration * deltaTime);
            position += _velocity * deltaTime;
            float floor = GroundHeight(position, ground);
            float ceiling = Mathf.Max(floor, _hasDestination ? _destination.y : floor) + _maxHeight;
            position.y = Mathf.Clamp(position.y, floor + _groundClearance, ceiling);
            if (position.y >= ceiling && _velocity.y > 0f)
            {
                _velocity.y = 0f;
            }

            transform.position = position;

            // Face the way it is heading, upright: the sway and climb move the body, not its facing
            Vector3 facing = _hasDestination ? (moving ? _heading : FlatToward(position, _destination)) : Vector3.zero;
            if (facing.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), _turnSpeed * deltaTime);
            }
        }

        private Vector3 FlightVelocity(Vector3 position, float ground)
        {
            float distance = FlatDistance(position, _destination);
            if (distance <= _arriveDistance)
            {
                return HoverVelocity(position, ground);
            }

            // Far off: aim at a point out on this flyer's own bearing so the swarm closes in from all around
            Vector3 aim = _destination;
            if (distance > _diveDistance + _approachRadius)
            {
                aim += _approachBearing * _approachRadius;
            }

            Vector3 flat = aim - position;
            flat.y = 0f;
            Vector3 forward = flat.sqrMagnitude > 0.0001f ? flat.normalized : transform.forward;
            _heading = forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward);

            // Sideways sway of about _weaveAmplitude metres (and half that up and down), each flyer on its own phase.
            // It fades out on the final approach so the flyer still arrives where it meant to
            float closeness = 1f - Mathf.Clamp01((distance - _arriveDistance) / _diveDistance);
            float omega = _weaveFrequency * Mathf.PI * 2f;
            float weave = _weaveAmplitude * omega * (1f - closeness);
            float sway = Mathf.Sin(Time.time * omega + _weaveSeed) * weave;
            float lift = Mathf.Sin(Time.time * omega * 0.7f + _weaveSeed * 1.3f) * weave * 0.5f;

            float speed = Mathf.Min(_speed, distance / 0.5f);
            Vector3 horizontal = forward * speed + side * sway;

            float arrive = _destination.y + _hoverHeight;
            float targetHeight = Mathf.Lerp(ground + Mathf.Min(_cruiseHeight, _maxHeight), arrive, closeness);
            float climb = Mathf.Clamp((targetHeight - position.y) * _heightFollowRate, -_speed, _speed) + lift;

            return new Vector3(horizontal.x, climb, horizontal.z);
        }

        private Vector3 HoverVelocity(Vector3 position, float ground)
        {
            float anchor = _hasDestination ? Mathf.Max(_destination.y, ground) + _hoverHeight : ground + _hoverHeight;
            float bob = Mathf.Sin(Time.time * _bobFrequency * Mathf.PI * 2f + _bobPhase) * _bobAmplitude;
            return new Vector3(0f, (anchor + bob - position.y) * _heightFollowRate, 0f);
        }

        private Vector3 Separation(Vector3 position)
        {
            if (_separationRadius <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 push = Vector3.zero;
            float sqrRadius = _separationRadius * _separationRadius;

            foreach (AI_FlyingMovement other in _active)
            {
                if (other == this || other == null)
                {
                    continue;
                }

                Vector3 away = position - other.transform.position;
                float sqr = away.sqrMagnitude;

                if (sqr < sqrRadius && sqr > 0.0001f)
                {
                    float distance = Mathf.Sqrt(sqr);
                    push += away / distance * (1f - distance / _separationRadius);
                }
            }

            return push;
        }

        private static float GroundHeight(Vector3 position, float fallback)
        {
            foreach (Terrain terrain in Terrain.activeTerrains)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;

                if (position.x >= origin.x && position.x <= origin.x + size.x && position.z >= origin.z && position.z <= origin.z + size.z)
                {
                    return origin.y + terrain.SampleHeight(position);
                }
            }

            return fallback;
        }

        private static Vector3 FlatToward(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            return direction;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private void OnDrawGizmosSelected()
        {
            if (_hasDestination && !_isStopped)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _destination);
            }
        }
    }
}
