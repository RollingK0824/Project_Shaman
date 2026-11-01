using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    // A spot a ghost can leave an anomaly at (NA_HauntAnomaly): a bloody handprint on a door, a pool of blood or filth on
    // the ground. The trace (Trace_Anomaly) shows the picked kind (its visual key) and lies along this transform:
    // forward = out of the door for a handprint, up for one on the ground. Placed by ProjectShaman/AI/Setup Anomaly Traces
    public class AnomalySpot : MonoBehaviour, IGhostHauntable
    {
        public const string HANDPRINT = "Handprint";
        public const string BLOOD = "Blood";
        public const string FILTH = "Filth";

        [Tooltip("이 자리에 남길 수 있는 이상 현상 (Handprint: 문, Blood·Filth: 바닥)")]
        [SerializeField] private List<string> _kinds = new List<string> { BLOOD, FILTH };

        [Header("Runtime (server only)")]
        [SerializeField] private int _hauntedNight = -1;
        [SerializeField] private string _lastKind;

        private readonly System.Random _random = new System.Random();

        public GhostTargetKind Kind => GhostTargetKind.AnomalySpot;
        public string LastKind => _lastKind;

        private void OnEnable()
        {
            GhostTargetRegistry.Register(this);
        }

        private void OnDisable()
        {
            GhostTargetRegistry.Unregister(this);
        }

        public bool CanBeTargeted(in GhostActionContext context)
        {
            return context.Verb == GhostActionVerb.Haunt && _hauntedNight != context.NightDay && _kinds.Count > 0;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        // Called just before the haunt lands, so the trace it leaves shows this kind
        public string PickKind()
        {
            _lastKind = _kinds.Count > 0 ? _kinds[_random.Next(_kinds.Count)] : BLOOD;
            return _lastKind;
        }

        public void ReceiveHaunt(in GhostActionContext context)
        {
            _hauntedNight = context.NightDay;
            AILog.Log(AILog.GHOST, context.GhostId, $"Haunt at {name}: {_lastKind}");
        }
    }
}
