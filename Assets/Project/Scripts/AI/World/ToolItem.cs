using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.World
{
    public class ToolItem : MonoBehaviour
    {
        private const float GROUND_SAMPLE_DISTANCE = 2f;

        [SerializeField] private string _toolInstanceId;
        [SerializeField] private string _toolTypeId;
        [SerializeField] private string _homeStoragePlaceId;
        [SerializeField] private Vector3 _heldLocalOffset = Vector3.zero;
        [SerializeField] private Vector3 _heldLocalEuler = Vector3.zero;

        [Header("Runtime")]
        [SerializeField] private string _holderId;
        [SerializeField] private string _reservedBy;

        private Transform _holderSocket;

        public string InstanceId => string.IsNullOrEmpty(_toolInstanceId) ? name : _toolInstanceId;
        public string TypeId => _toolTypeId;
        public string HomeStoragePlaceId => _homeStoragePlaceId;
        public string HolderId => _holderId;
        public string ReservedBy => _reservedBy;
        public bool IsHeld => !string.IsNullOrEmpty(_holderId);
        public bool IsOnGround => !IsHeld;
        public Vector3 Position => transform.position;

        private void OnEnable()
        {
            ToolRegistry.Register(this);
        }

        private void OnDisable()
        {
            ToolRegistry.Unregister(this);
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

            _holderId = villagerId;
            _reservedBy = null;
            _holderSocket = socket;
            FollowSocket();
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

            if (NavMesh.SamplePosition(position, out NavMeshHit hit, GROUND_SAMPLE_DISTANCE, NavMesh.AllAreas))
            {
                position = hit.position;
            }

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            AILog.Log(AILog.TOOL, villagerId, $"Dropped {InstanceId}({TypeId}) at {position} ({reason})");
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
