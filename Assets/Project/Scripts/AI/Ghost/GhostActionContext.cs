using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Ghost
{
    public readonly struct GhostActionContext
    {
        public readonly string GhostId;
        public readonly AI_Possession Possession;
        public readonly AI_GhostEntity Entity;
        public readonly NightOutcomeLedger Ledger;
        public readonly GhostActionVerb Verb;
        public readonly Vector3 Center;
        public readonly float Radius;

        public GhostActionContext(AI_Possession possession, AI_GhostEntity entity, NightOutcomeLedger ledger, GhostActionVerb verb, Vector3 center, float radius)
        {
            GhostId = possession != null ? possession.GhostId : string.Empty;
            Possession = possession;
            Entity = entity;
            Ledger = ledger;
            Verb = verb;
            Center = center;
            Radius = radius;
        }

        public bool IsInsideRadius(Vector3 position)
        {
            Vector3 offset = position - Center;
            offset.y = 0f;
            return offset.sqrMagnitude <= Radius * Radius;
        }
    }
}
