using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI.Ghost
{
    public static class GhostTargetRegistry
    {
        private static readonly List<IGhostTarget> _targets = new List<IGhostTarget>();

        public static int Count => _targets.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _targets.Clear();
        }

        public static void Register(IGhostTarget target)
        {
            if (target != null && !_targets.Contains(target))
            {
                _targets.Add(target);
            }
        }

        public static void Unregister(IGhostTarget target)
        {
            _targets.Remove(target);
        }

        public static bool HasAny(in GhostTargetQuery query, in GhostActionContext context)
        {
            foreach (IGhostTarget target in _targets)
            {
                if (IsCandidate(target, query, context))
                {
                    return true;
                }
            }

            return false;
        }

        public static IGhostTarget Find(in GhostTargetQuery query, Vector3 from, in GhostActionContext context)
        {
            IGhostTarget best = null;
            int bestPriority = int.MinValue;
            int bestRank = int.MaxValue;
            float bestDistance = float.MaxValue;

            foreach (IGhostTarget target in _targets)
            {
                if (!IsCandidate(target, query, context))
                {
                    continue;
                }

                int priority = query.PreferMarked ? target.GetPriority(context) : 0;
                int rank = query.RankOf(target.Kind);
                float distance = (target.gameObject.transform.position - from).sqrMagnitude;

                bool isBetter = priority > bestPriority
                    || (priority == bestPriority && rank < bestRank)
                    || (priority == bestPriority && rank == bestRank && distance < bestDistance);

                if (isBetter)
                {
                    best = target;
                    bestPriority = priority;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static bool IsCandidate(IGhostTarget target, in GhostTargetQuery query, in GhostActionContext context)
        {
            if (!IsAlive(target) || query.RankOf(target.Kind) < 0)
            {
                return false;
            }

            Vector3 position = target.gameObject.transform.position;
            return context.IsInsideRadius(position) && !GhostBarrierRegistry.IsBlocked(position, context.Verb) && target.CanBeTargeted(context);
        }

        private static bool IsAlive(IGhostTarget target)
        {
            return target != null && !(target is Object unityObject && unityObject == null);
        }
    }
}
