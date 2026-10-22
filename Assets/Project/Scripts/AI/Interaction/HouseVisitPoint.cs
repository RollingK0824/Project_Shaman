using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;

namespace ProjectShaman.AI.Interaction
{
    // Where a neighbour stands to look into a house on the way home. One per house owner, created on demand (server).
    // Not registered for scanning: villagers only come here when they decide to visit.
    public class HouseVisitPoint : MonoBehaviour, IVillagerInteractable
    {
        private const float LOOK_HEIGHT = 1.2f;

        private static readonly Dictionary<string, HouseVisitPoint> _byOwner = new Dictionary<string, HouseVisitPoint>();
        private static InteractionProfile _profile;

        private string _ownerId;

        public string InteractableId => $"House_{_ownerId}";
        public InteractionProfile Profile => _profile != null ? _profile : (_profile = CreateProfile());
        public Vector3 LookPoint => transform.position + Vector3.up * LOOK_HEIGHT;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _byOwner.Clear();
        }

        public static HouseVisitPoint For(string ownerId, Vector3 homePosition)
        {
            if (_byOwner.TryGetValue(ownerId, out HouseVisitPoint point) && point != null)
            {
                return point;
            }

            GameObject go = new GameObject($"HouseVisit_{ownerId}");
            go.transform.position = homePosition;
            point = go.AddComponent<HouseVisitPoint>();
            point._ownerId = ownerId;
            _byOwner[ownerId] = point;
            return point;
        }

        public static void ClearAll()
        {
            foreach (HouseVisitPoint point in _byOwner.Values)
            {
                if (point != null)
                {
                    Destroy(point.gameObject);
                }
            }

            _byOwner.Clear();
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
            profile.name = "HouseVisit";
            profile.hideFlags = HideFlags.DontSave;
            return profile;
        }
    }
}
