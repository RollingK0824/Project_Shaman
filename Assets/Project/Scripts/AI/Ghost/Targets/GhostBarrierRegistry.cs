using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public static class GhostBarrierRegistry
    {
        private static readonly List<IGhostBarrier> _barriers = new List<IGhostBarrier>();

        public static int Count => _barriers.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _barriers.Clear();
        }

        public static void Register(IGhostBarrier barrier)
        {
            if (barrier != null && !_barriers.Contains(barrier))
            {
                _barriers.Add(barrier);
            }
        }

        public static void Unregister(IGhostBarrier barrier)
        {
            _barriers.Remove(barrier);
        }

        public static bool IsBlocked(Vector3 position, GhostActionVerb verb)
        {
            foreach (IGhostBarrier barrier in _barriers)
            {
                if (barrier != null && !(barrier is Object unityObject && unityObject == null) && barrier.Blocks(position, verb))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
