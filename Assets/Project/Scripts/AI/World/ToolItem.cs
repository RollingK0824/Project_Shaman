using System;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class ToolItem : MonoBehaviour, IGhostCarriable
    {
        private const float GROUND_SAMPLE_DISTANCE = 2f;
        private const float GROUND_CLEARANCE = 0.01f;
        private const float SETTLE_MIN_SECONDS = 0.2f;

        [SerializeField] private string _toolInstanceId;
        [SerializeField] private string _toolTypeId;
        [SerializeField] private string _homeStoragePlaceId;
        [SerializeField] private ItemData _itemData;
        [SerializeField] private Vector3 _heldLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _heldLocalEuler = Vector3.zero;
        [Tooltip("Turn the collider off while a player, villager or ghost holds the tool")]
        [SerializeField] private bool _disableColliderWhenHeld = true;

        [Header("Holder")]
        [SerializeField] private Vector3 _holderLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _holderLocalEuler = Vector3.zero;

        [Header("Physics")]
        [Tooltip("A dropped or leaned tool falls on the server for at most this long, then stays put")]
        [SerializeField, Min(0f)] private float _settleMaxSeconds = 1.5f;
        [SerializeField, Min(0.01f)] private float _mass = 1.5f;

        [Header("Runtime")]
        [SerializeField] private string _holderId;
        [SerializeField] private string _reservedBy;
        [SerializeField] private int _ghostHiddenNight = -1;
        [SerializeField] private ToolHolder _holder;
        [SerializeField] private int _holderSlot = -1;

        private Transform _holderSocket;
        private Collider _collider;
        private Rigidbody _rigidbody;
        private Renderer[] _renderers;
        private float _settleStart = -1f;

        public string InstanceId => string.IsNullOrEmpty(_toolInstanceId) ? name : _toolInstanceId;
        public string TypeId => _toolTypeId;
        public string HomeStoragePlaceId => _homeStoragePlaceId;
        public ItemData ItemData => _itemData;
        public string HolderId => _holderId;
        public string ReservedBy => _reservedBy;
        public bool IsHeld => !string.IsNullOrEmpty(_holderId);
        public bool IsOnGround => !IsHeld;
        // On the ground and not claimed by a villager walking over to pick it up
        public bool IsUnclaimed => IsOnGround && string.IsNullOrEmpty(_reservedBy);
        public Vector3 Position => transform.position;
        public GhostTargetKind Kind => GhostTargetKind.Tool;
        public ToolHolder Holder => _holder;
        public bool IsSettling => _settleStart >= 0f;
        public bool SimulatesPhysics { get; set; } = true;

        public event Action<ToolItem> OnSettled;

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            _renderers = GetComponentsInChildren<Renderer>(true);

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.mass = _mass;
            }

            _rigidbody.isKinematic = true;
            _rigidbody.interpolation = RigidbodyInterpolation.None;
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        public void ApplyStowedState(bool isStowed)
        {
            foreach (Renderer visual in _renderers)
            {
                if (visual != null)
                {
                    visual.enabled = !isStowed;
                }
            }
        }

        // The server applies it on pick up and drop; clients get the held state from the network layer
        public void ApplyHeldState(bool isHeld)
        {
            if (_collider != null)
            {
                _collider.enabled = !(_disableColliderWhenHeld && isHeld);
            }
        }

        private void OnEnable()
        {
            ToolRegistry.Register(this);
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            ToolRegistry.Unregister(this);
            GhostTargetRegistry.Unregister(this);
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            return context.Verb == GhostActionVerb.HideTool && IsAvailableFor(context.GhostId) && _ghostHiddenNight != context.NightDay
                && (context.Entity == null || context.Entity.CanCarryInTime(context.Action, transform.position));
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        public bool PickUpByGhost(in GhostActionContext context)
        {
            Transform socket = context.Entity != null ? context.Entity.CarrySocket : transform;
            if (!TryPickUp(context.GhostId, socket))
            {
                return false;
            }

            _ghostHiddenNight = context.NightDay;
            return true;
        }

        public void DropByGhost(in GhostActionContext context, Vector3 position)
        {
            Drop(context.GhostId, position, $"hidden by ghost ({context.ActionId})");
        }

        public bool IsAvailableFor(string villagerId)
        {
            return IsOnGround && (string.IsNullOrEmpty(_reservedBy) || _reservedBy == villagerId);
        }

        public bool TryReserve(string villagerId)
        {
            if (!IsAvailableFor(villagerId))
            {
                return false;
            }

            _reservedBy = villagerId;
            return true;
        }

        public void CancelReservation(string villagerId)
        {
            if (_reservedBy == villagerId)
            {
                _reservedBy = null;
            }
        }

        public bool TryPickUp(string villagerId, Transform socket)
        {
            if (!IsAvailableFor(villagerId))
            {
                return false;
            }

            StopSettle();
            LeaveHolder();
            _holderId = villagerId;
            _reservedBy = null;
            _holderSocket = socket;
            FollowSocket();
            ApplyHeldState(true);
            AILog.Log(AILog.TOOL, villagerId, $"Picked up {InstanceId}({TypeId})");
            return true;
        }

        public void Drop(string villagerId, Vector3 position, string reason)
        {
            if (_holderId != villagerId)
            {
                return;
            }

            _holderId = null;
            _holderSocket = null;
            ApplyHeldState(false);

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, GROUND_SAMPLE_DISTANCE, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            LiftAboveGround(position.y);
            AILog.Log(AILog.TOOL, villagerId, $"Dropped {InstanceId}({TypeId}) at {position} ({reason})");
            StartSettle();
        }

        public bool PlaceInHolder(string villagerId, ToolHolder holder, int slot, string reason)
        {
            Transform slotTransform = holder != null ? holder.GetSlot(slot) : null;
            if (_holderId != villagerId || slotTransform == null)
            {
                return false;
            }

            _holderId = null;
            _holderSocket = null;
            ApplyHeldState(false);
            _holder = holder;
            _holderSlot = slot;

            transform.SetPositionAndRotation(
                slotTransform.TransformPoint(_holderLocalOffset),
                slotTransform.rotation * Quaternion.Euler(_holderLocalEuler));
            AILog.Log(AILog.TOOL, villagerId, $"Placed {InstanceId}({TypeId}) in {holder.HolderId} slot {slot} ({holder.Mode}, {reason})");

            if (holder.Mode == ToolHolderMode.Lean)
            {
                StartSettle();
            }
            else
            {
                OnSettled?.Invoke(this);
            }

            return true;
        }

        private void LeaveHolder()
        {
            if (_holder != null)
            {
                _holder.Release(this);
            }

            _holder = null;
            _holderSlot = -1;
        }

        private void LiftAboveGround(float groundY)
        {
            if (_collider == null || !_collider.enabled)
            {
                return;
            }

            Physics.SyncTransforms();
            float sink = groundY - _collider.bounds.min.y;
            if (sink > 0f)
            {
                transform.position += Vector3.up * (sink + GROUND_CLEARANCE);
            }
        }

        private void StartSettle()
        {
            if (!SimulatesPhysics || _settleMaxSeconds <= 0f || _collider == null || !_collider.enabled)
            {
                OnSettled?.Invoke(this);
                return;
            }

            _settleStart = Time.time;
            _rigidbody.isKinematic = false;
            _rigidbody.WakeUp();
        }

        private void StopSettle()
        {
            if (_settleStart < 0f)
            {
                return;
            }

            _settleStart = -1f;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        private void FixedUpdate()
        {
            if (_settleStart < 0f)
            {
                return;
            }

            float elapsed = Time.time - _settleStart;
            if (elapsed < _settleMaxSeconds && (elapsed < SETTLE_MIN_SECONDS || !_rigidbody.IsSleeping()))
            {
                return;
            }

            StopSettle();
            if (!NavMesh.SamplePosition(transform.position, out _, GROUND_SAMPLE_DISTANCE, NavMesh.AllAreas))
            {
                AILog.Warn(AILog.TOOL, $"{InstanceId}({TypeId}) settled off the NavMesh at {transform.position}, villagers may not reach it");
            }

            OnSettled?.Invoke(this);
        }

        private void LateUpdate()
        {
            if (_holderSocket != null)
            {
                FollowSocket();
            }
        }

        private void FollowSocket()
        {
            transform.SetPositionAndRotation(
                _holderSocket.TransformPoint(_heldLocalOffset),
                _holderSocket.rotation * Quaternion.Euler(_heldLocalEuler));
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsHeld ? Color.magenta : (string.IsNullOrEmpty(_reservedBy) ? Color.yellow : new Color(1f, 0.5f, 0f));
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        }
    }
}
