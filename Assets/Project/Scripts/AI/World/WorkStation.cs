using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.World
{
    public class WorkStation : MonoBehaviour
    {
        [SerializeField] private string _stationId;
        [SerializeField] private Transform _standPoint;
        [SerializeField] private Transform _lookTarget;
        [SerializeField] private List<string> _acceptedActionIds = new List<string>();

        [Header("Runtime")]
        [SerializeField] private string _occupantId;
        [SerializeField] private int _acceptedHitCount;

        public string StationId => string.IsNullOrEmpty(_stationId) ? name : _stationId;
        public string OccupantId => _occupantId;
        public bool IsAvailable => string.IsNullOrEmpty(_occupantId);
        public Vector3 StandPosition => _standPoint != null ? _standPoint.position : transform.position;
        public Vector3 LookPosition => _lookTarget != null ? _lookTarget.position : transform.position;

        public event System.Action<string, string> OnWorkAccepted;
        public event System.Action<string, string> OnWorkRejected;

        public bool Accepts(string actionId)
        {
            return _acceptedActionIds.Count == 0 || (!string.IsNullOrEmpty(actionId) && _acceptedActionIds.Contains(actionId));
        }

        public bool TryReserve(string occupantId)
        {
            if (!IsAvailable && _occupantId != occupantId)
            {
                return false;
            }

            _occupantId = occupantId;
            AILog.Log(AILog.WORK, occupantId, $"Station {StationId} reserved");
            return true;
        }

        public void Release(string occupantId)
        {
            if (_occupantId != occupantId)
            {
                return;
            }

            _occupantId = null;
            AILog.Log(AILog.WORK, occupantId, $"Station {StationId} released");
        }

        public void ReceiveWork(string actorId, string actionId)
        {
            if (!Accepts(actionId))
            {
                AILog.Log(AILog.WORK, actorId, $"Station {StationId} rejected action '{actionId}'");
                OnWorkRejected?.Invoke(actorId, actionId);
                return;
            }

            _acceptedHitCount++;
            AILog.Log(AILog.WORK, actorId, $"Station {StationId} hit by '{actionId}' (#{_acceptedHitCount})");
            OnWorkAccepted?.Invoke(actorId, actionId);
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsAvailable ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(StandPosition, 0.3f);
            Gizmos.DrawLine(StandPosition, LookPosition);
        }
    }
}
