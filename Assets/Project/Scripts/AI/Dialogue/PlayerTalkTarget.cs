using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;

namespace ProjectShaman.AI.Dialogue
{
    // Lets a villager hold still and face a talking player through the normal interaction branch (Stop response).
    // Added to the player on the server when a dialogue starts; never registered, so villagers do not scan it.
    public class PlayerTalkTarget : MonoBehaviour, IVillagerInteractable
    {
        private const float LOOK_HEIGHT = 1.6f;

        private static InteractionProfile _profile;

        public string InteractableId => $"Player_{name}";
        public InteractionProfile Profile => _profile != null ? _profile : (_profile = CreateProfile());
        public Vector3 LookPoint => transform.position + Vector3.up * LOOK_HEIGHT;

        public static PlayerTalkTarget For(GameObject player)
        {
            return player.TryGetComponent(out PlayerTalkTarget target) ? target : player.AddComponent<PlayerTalkTarget>();
        }

        public bool CanInteract(in InteractionContext context)
        {
            return true;
        }

        public void BeginInteraction(in InteractionContext context, InteractionResponse response)
        {
        }

        public InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason)
        {
            return InteractionResult.None(InteractableId, InteractionKind.Conversation, response);
        }

        private static InteractionProfile CreateProfile()
        {
            InteractionProfile profile = ScriptableObject.CreateInstance<InteractionProfile>();
            profile.name = "PlayerTalk";
            profile.hideFlags = HideFlags.DontSave;
            return profile;
        }
    }
}
