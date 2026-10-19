using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI.Interaction
{
    public static class InteractableRegistry
    {
        private static readonly List<IVillagerInteractable> _interactables = new List<IVillagerInteractable>();

        public static int Count => _interactables.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _interactables.Clear();
        }

        public static void Register(IVillagerInteractable interactable)
        {
            if (interactable != null && !_interactables.Contains(interactable))
            {
                _interactables.Add(interactable);
            }
        }

        public static void Unregister(IVillagerInteractable interactable)
        {
            _interactables.Remove(interactable);
        }

        public static void CollectNoticeable(in InteractionContext context, float defaultRadius, List<IVillagerInteractable> results)
        {
            results.Clear();

            foreach (IVillagerInteractable interactable in _interactables)
            {
                if (IsNoticeable(interactable, context, defaultRadius))
                {
                    results.Add(interactable);
                }
            }

            Vector3 from = context.Position;
            results.Sort((a, b) => Compare(a, b, from));
        }

        public static bool IsNoticeable(IVillagerInteractable interactable, in InteractionContext context, float defaultRadius)
        {
            if (!IsAlive(interactable) || interactable.Profile == null)
            {
                return false;
            }

            float radius = interactable.Profile.HasNoticeRadius ? interactable.Profile.NoticeRadius : defaultRadius;
            if ((interactable.gameObject.transform.position - context.Position).sqrMagnitude > radius * radius)
            {
                return false;
            }

            return interactable.CanInteract(context);
        }

        private static int Compare(IVillagerInteractable a, IVillagerInteractable b, Vector3 from)
        {
            int priorityA = a.Profile.Priority;
            int priorityB = b.Profile.Priority;

            if (priorityA != priorityB)
            {
                return priorityB.CompareTo(priorityA);
            }

            float distanceA = (a.gameObject.transform.position - from).sqrMagnitude;
            float distanceB = (b.gameObject.transform.position - from).sqrMagnitude;
            return distanceA.CompareTo(distanceB);
        }

        public static bool IsAlive(IVillagerInteractable interactable)
        {
            return interactable != null && !(interactable is Object unityObject && unityObject == null);
        }
    }
}
