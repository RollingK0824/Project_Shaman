using System;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;

namespace ProjectShaman.AI.World
{
    public class ShadowSpot : MonoBehaviour, IGhostAbsorbable
    {
        [Header("Runtime (server only)")]
        [SerializeField] private int _absorbedNight = -1;

        public event Action<ShadowSpot> OnAbsorbed;

        public GhostTargetKind Kind => GhostTargetKind.ShadowSpot;

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
            return context.Verb == GhostActionVerb.Absorb && _absorbedNight != context.NightDay;
        }

        public int GetPriority(in GhostActionContext context)
        {
            return 0;
        }

        public void ReceiveAbsorb(in GhostActionContext context)
        {
            _absorbedNight = context.NightDay;
            AILog.Log(AILog.GHOST, context.GhostId, $"Absorbed yin at {name}");
            OnAbsorbed?.Invoke(this);
        }
    }
}
