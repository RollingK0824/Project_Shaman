using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class AnomalySpot : MonoBehaviour, IGhostHauntable
    {
        [Header("Runtime (server only)")]
        [SerializeField] private int _hauntedNight = -1;

        public GhostTargetKind Kind => GhostTargetKind.AnomalySpot;

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
            return context.Verb == GhostActionVerb.Haunt && _hauntedNight != context.NightDay;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        public void ReceiveHaunt(in GhostActionContext context)
        {
            _hauntedNight = context.NightDay;
            AILog.Log(AILog.STUB, context.GhostId, $"Haunt at {name} (effect not designed yet)");
        }
    }
}
