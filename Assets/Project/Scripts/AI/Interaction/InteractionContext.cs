using UnityEngine;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    public readonly struct InteractionContext
    {
        public readonly string VillagerId;
        public readonly Vector3 Position;
        public readonly VillagerActivity Activity;
        public readonly int DayCount;
        public readonly float GameNow;

        public InteractionContext(string villagerId, Vector3 position, VillagerActivity activity, int dayCount, float gameNow)
        {
            VillagerId = villagerId;
            Position = position;
            Activity = activity;
            DayCount = dayCount;
            GameNow = gameNow;
        }
    }

    public readonly struct InteractionResult
    {
        public readonly string InteractableId;
        public readonly InteractionKind Kind;
        public readonly InteractionResponse Response;
        public readonly bool Witnessed;
        public readonly bool Detailed;
        public readonly float Stress;
        public readonly string Subject;
        public readonly string PlaceId;
        public readonly Vector3 Position;

        public InteractionResult(string interactableId, InteractionKind kind, InteractionResponse response, bool witnessed, bool detailed, float stress,
            string subject = null, string placeId = null, Vector3 position = default)
        {
            InteractableId = interactableId;
            Kind = kind;
            Response = response;
            Witnessed = witnessed;
            Detailed = detailed;
            Stress = stress;
            Subject = string.IsNullOrEmpty(subject) ? interactableId : subject;
            PlaceId = placeId;
            Position = position;
        }

        public static InteractionResult None(string interactableId, InteractionKind kind, InteractionResponse response)
        {
            return new InteractionResult(interactableId, kind, response, false, false, 0f);
        }

        public override string ToString()
        {
            return $"{Kind}:{InteractableId} {Response} witnessed={Witnessed} detailed={Detailed} stress={Stress:F1}";
        }
    }
}
