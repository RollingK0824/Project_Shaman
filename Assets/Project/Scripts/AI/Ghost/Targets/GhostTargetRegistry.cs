using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public static class GhostTargetRegistry
    {
        private static readonly List<IGhostTarget> _targets = new List<IGhostTarget>();
        private static readonly Dictionary<IGhostTarget, string> _claims = new Dictionary<IGhostTarget, string>();
        private static readonly List<IGhostTarget> _releaseBuffer = new List<IGhostTarget>();

        public static int Count => _targets.Count;
        public static int ClaimCount => _claims.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _targets.Clear();
            _claims.Clear();
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

            if (target != null)
            {
                _claims.Remove(target);
            }
        }

        public static bool TryClaim(IGhostTarget target, string ghostId)
        {
            if (target == null || string.IsNullOrEmpty(ghostId) || IsClaimedByOther(target, ghostId))
            {
                return false;
            }

            _claims[target] = ghostId;
            return true;
        }

        public static void Release(IGhostTarget target, string ghostId)
        {
            if (target != null && _claims.TryGetValue(target, out string owner) && owner == ghostId)
            {
                _claims.Remove(target);
            }
        }

        public static void ReleaseAll(string ghostId)
        {
            _releaseBuffer.Clear();

            foreach (KeyValuePair<IGhostTarget, string> claim in _claims)
            {
                if (claim.Value == ghostId)
                {
                    _releaseBuffer.Add(claim.Key);
                }
            }

            foreach (IGhostTarget target in _releaseBuffer)
            {
                _claims.Remove(target);
            }

            _releaseBuffer.Clear();
        }

        public static bool IsClaimedByOther(IGhostTarget target, string ghostId)
        {
            return target != null && _claims.TryGetValue(target, out string owner) && owner != ghostId;
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

                int priority = GetPriority(target, query, context);
                int rank = query.RankOf(target.Kind);
                float distance = (target.gameObject.transform.position - from).sqrMagnitude;

                if (Compare(priority, rank, distance, bestPriority, bestRank, bestDistance) < 0)
                {
                    best = target;
                    bestPriority = priority;
                    bestRank = rank;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public static void CollectCandidates(in GhostTargetQuery query, Vector3 from, in GhostActionContext context, List<IGhostTarget> results)
        {
            results.Clear();

            foreach (IGhostTarget target in _targets)
            {
                if (IsCandidate(target, query, context))
                {
                    results.Add(target);
                }
            }

            GhostTargetQuery sortQuery = query;
            GhostActionContext sortContext = context;

            results.Sort((a, b) => Compare(
                GetPriority(a, sortQuery, sortContext), sortQuery.RankOf(a.Kind), (a.gameObject.transform.position - from).sqrMagnitude,
                GetPriority(b, sortQuery, sortContext), sortQuery.RankOf(b.Kind), (b.gameObject.transform.position - from).sqrMagnitude));
        }

        public static void CollectOfKind(GhostTargetKind kind, List<IGhostTarget> results)
        {
            results.Clear();

            foreach (IGhostTarget target in _targets)
            {
                if (IsAlive(target) && target.Kind == kind)
                {
                    results.Add(target);
                }
            }
        }

        private static int GetPriority(IGhostTarget target, in GhostTargetQuery query, in GhostActionContext context)
        {
            return query.PreferMarked ? target.GetPriority(context) : 0;
        }

        private static int Compare(int priorityA, int rankA, float distanceA, int priorityB, int rankB, float distanceB)
        {
            if (priorityA != priorityB)
            {
                return priorityB.CompareTo(priorityA);
            }

            if (rankA != rankB)
            {
                return rankA.CompareTo(rankB);
            }

            return distanceA.CompareTo(distanceB);
        }

        public static bool IsCandidate(IGhostTarget target, in GhostTargetQuery query, in GhostActionContext context)
        {
            if (!IsAlive(target) || query.RankOf(target.Kind) < 0 || IsClaimedByOther(target, context.GhostId))
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
