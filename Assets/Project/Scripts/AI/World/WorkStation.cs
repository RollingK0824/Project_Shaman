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

        [Header("Reset after each hit (장작: 쪼개짐 → 사라짐 → 새 장작)")]
        [Tooltip("한 번 맞은 뒤 새로 준비될 때까지 시간(초). 0이면 계속 칠 수 있음 (모루·빨랫돌)")]
        [SerializeField, Min(0f)] private float _resetSeconds;
        [Tooltip("맞은 뒤 쪼개진 모습을 보여 주는 시간(초). 지나면 Reset Visual을 숨김")]
        [SerializeField, Min(0f)] private float _splitShowSeconds = 0.6f;
        [Tooltip("다시 준비될 때까지 숨겼다가 다시 보여 줄 모델 (장작)")]
        [SerializeField] private GameObject _resetVisual;

        [Header("Runtime")]
        [SerializeField] private string _occupantId;
        [SerializeField] private int _acceptedHitCount;

        // [Server] Time.time of the hide and of being ready again; -1 when nothing is pending
        private float _hideAt = -1f;
        private float _readyAt = -1f;

        public string StationId => string.IsNullOrEmpty(_stationId) ? name : _stationId;
        public string OccupantId => _occupantId;
        public bool IsAvailable => string.IsNullOrEmpty(_occupantId);
        public Vector3 StandPosition => _standPoint != null ? _standPoint.position : transform.position;
        public Vector3 LookPosition => _lookTarget != null ? _lookTarget.position : transform.position;

        public event System.Action<string, string> OnWorkAccepted;
        public event System.Action<string, string> OnWorkRejected;
        // [Server] the reset visual was hidden (false) or set up again (true): Net_WorkStation shows it on every client
        public event System.Action<bool> OnResetVisualChanged;

        // [Server] Set up for the next hit. A worker waits (stands) until it is
        public bool IsReady => _readyAt < 0f;
        public bool ResetsAfterHit => _resetSeconds > 0f;

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
            if (!IsReady)
            {
                return;
            }

            if (!Accepts(actionId))
            {
                AILog.Log(AILog.WORK, actorId, $"Station {StationId} rejected action '{actionId}'");
                OnWorkRejected?.Invoke(actorId, actionId);
                return;
            }

            _acceptedHitCount++;
            OnWorkAccepted?.Invoke(actorId, actionId);

            if (ResetsAfterHit)
            {
                _hideAt = Time.time + _splitShowSeconds;
                _readyAt = _hideAt + _resetSeconds;
            }
        }

        private void Update()
        {
            if (_hideAt >= 0f && Time.time >= _hideAt)
            {
                _hideAt = -1f;
                SetResetVisual(false);
            }

            if (_readyAt >= 0f && Time.time >= _readyAt)
            {
                _readyAt = -1f;
                SetResetVisual(true);
            }
        }

        private void SetResetVisual(bool shown)
        {
            ApplyResetVisual(shown);
            OnResetVisualChanged?.Invoke(shown);
        }

        // Every peer: re-enabling also starts its Animator over (the split log is whole again)
        public void ApplyResetVisual(bool shown)
        {
            if (_resetVisual != null && _resetVisual.activeSelf != shown)
            {
                _resetVisual.SetActive(shown);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = IsAvailable ? Color.cyan : Color.red;
            Gizmos.DrawWireSphere(StandPosition, 0.3f);
            Gizmos.DrawLine(StandPosition, LookPosition);
        }
    }
}
