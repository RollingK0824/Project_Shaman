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
        // A player come about a request (an offer to take, a tool brought back) stops a villager busy with an everyday
        // interaction (a look-in on the way home, 50), not one shaken by a body (80+)
        private const int REQUEST_PRIORITY = 70;

        private static InteractionProfile _profile;
        private static InteractionProfile _requestProfile;

        private bool _isRequest;

        public string InteractableId => $"Player_{name}";
        public InteractionProfile Profile => _isRequest
            ? (_requestProfile != null ? _requestProfile : (_requestProfile = CreateProfile("PlayerRequest", REQUEST_PRIORITY)))
            : (_profile != null ? _profile : (_profile = CreateProfile("PlayerTalk", -1)));
        public Vector3 LookPoint => transform.position + Vector3.up * LOOK_HEIGHT;

        public static PlayerTalkTarget For(GameObject player)
        {
            return Get(player, false);
        }

        public static PlayerTalkTarget ForRequest(GameObject player)
        {
            return Get(player, true);
        }

        private static PlayerTalkTarget Get(GameObject player, bool isRequest)
        {
            PlayerTalkTarget target = player.TryGetComponent(out PlayerTalkTarget existing) ? existing : player.AddComponent<PlayerTalkTarget>();
            target._isRequest = isRequest;
            return target;
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

        // priority < 0 keeps the profile's default
        private static InteractionProfile CreateProfile(string profileName, int priority)
        {
            InteractionProfile profile = ScriptableObject.CreateInstance<InteractionProfile>();
            profile.name = profileName;
            profile.hideFlags = HideFlags.DontSave;
            if (priority >= 0)
            {
                profile.SetPriority(priority);
            }

            return profile;
        }
    }
}
