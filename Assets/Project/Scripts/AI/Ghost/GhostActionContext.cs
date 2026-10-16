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
        public readonly GhostNightActionData Action;
        public readonly GhostActionVerb Verb;
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly bool IgnoreRadius;

        public GhostActionContext(AI_Possession possession, AI_GhostEntity entity, NightOutcomeLedger ledger, GhostNightActionData action, Vector3 center, float radius, bool ignoreRadius)
        {
            IgnoreRadius = ignoreRadius;
            GhostId = possession != null ? possession.GhostId : string.Empty;
            Possession = possession;
            Entity = entity;
            Ledger = ledger;
            Action = action;
            Verb = action != null ? action.Verb : GhostActionVerb.None;
            Center = center;
            Radius = radius;
        }

        public string ActionId => Action != null ? Action.ActionId : Verb.ToString();
        public int NightDay => Ledger != null ? Ledger.NightDay : -1;

        public bool IsInsideRadius(Vector3 position)
        {
            if (IgnoreRadius)
            {
                return true;
            }

            Vector3 offset = position - Center;
            offset.y = 0f;
            return offset.sqrMagnitude <= Radius * Radius;
        }
    }
}
