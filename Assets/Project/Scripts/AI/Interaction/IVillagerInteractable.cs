using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    public interface IVillagerInteractable
    {
        string InteractableId { get; }
        InteractionProfile Profile { get; }
        Vector3 LookPoint { get; }
        GameObject gameObject { get; }
        bool CanInteract(in InteractionContext context);
        void BeginInteraction(in InteractionContext context, InteractionResponse response);
        InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason);
    }
}
