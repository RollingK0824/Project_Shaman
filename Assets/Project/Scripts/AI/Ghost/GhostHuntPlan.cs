using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;

namespace ProjectShaman.AI.Ghost
{
    public class GhostHuntPlan
    {
        private readonly string _ghostId;
        private readonly List<IGhostTarget> _targets = new List<IGhostTarget>();
        private readonly List<IGhostTarget> _candidates = new List<IGhostTarget>();

        public GhostHuntPlan(string ghostId, int kills, bool ignoresRadius)
        {
            _ghostId = ghostId;
            KillsRemaining = Mathf.Max(0, kills);
            IgnoresRadius = KillsRemaining > 0 && ignoresRadius;
        }

        public int KillsRemaining { get; private set; }
        public bool IgnoresRadius { get; }
        public bool HasKills => KillsRemaining > 0;
        public int TargetCount => _targets.Count;

        public bool Contains(IGhostTarget target)
        {
            return target != null && _targets.Contains(target);
        }

        public void Refill(in GhostTargetQuery query, Vector3 from, in GhostActionContext context)
        {
            for (int i = _targets.Count - 1; i >= 0; i--)
            {
                IGhostTarget target = _targets[i];
                if (!GhostTargetRegistry.IsCandidate(target, query, context))
                {
                    _targets.RemoveAt(i);
                    GhostTargetRegistry.Release(target, _ghostId);
                    AILog.Log(AILog.GHOST, _ghostId, $"Hunt target {NameOf(target)} dropped (no longer valid)");
                }
            }

            if (_targets.Count >= KillsRemaining)
            {
                return;
            }

            GhostTargetRegistry.CollectCandidates(query, from, context, _candidates);

            foreach (IGhostTarget candidate in _candidates)
            {
                if (_targets.Count >= KillsRemaining)
                {
                    break;
                }

                if (!_targets.Contains(candidate) && GhostTargetRegistry.TryClaim(candidate, _ghostId))
                {
                    _targets.Add(candidate);
                    AILog.Log(AILog.GHOST, _ghostId, $"Hunt target reserved {NameOf(candidate)} ({_targets.Count}/{KillsRemaining})");
                }
            }

            _candidates.Clear();
        }

        public IGhostTarget PickNearest(Vector3 from)
        {
            IGhostTarget best = null;
            float bestDistance = float.MaxValue;

            foreach (IGhostTarget target in _targets)
            {
                float distance = (target.gameObject.transform.position - from).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = target;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public void OnKilled(IGhostTarget target)
        {
            _targets.Remove(target);
            GhostTargetRegistry.Release(target, _ghostId);
            KillsRemaining = Mathf.Max(0, KillsRemaining - 1);
        }

        public int Abandon()
        {
            int lost = KillsRemaining;
            KillsRemaining = 0;
            _targets.Clear();
            return lost;
        }

        public void WriteDebug(List<string> result)
        {
            result.Clear();

            foreach (IGhostTarget target in _targets)
            {
                result.Add(NameOf(target));
            }
        }

        private static string NameOf(IGhostTarget target)
        {
            return target != null && !(target is Object unityObject && unityObject == null) ? target.gameObject.name : "(gone)";
        }
    }
}
