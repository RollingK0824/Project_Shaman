using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;

namespace ProjectShaman.AI.Mock
{
    public class MockInteractable : MonoBehaviour, IVillagerInteractable
    {
        [SerializeField] private string _interactableId;
        [SerializeField] private InteractionProfile _profile;
        [SerializeField] private Transform _lookPoint;
        [SerializeField] private bool _canInteract = true;

        private int _generation;

        public string InteractableId => $"{(string.IsNullOrEmpty(_interactableId) ? name : _interactableId)}#{_generation}";
        public InteractionProfile Profile => _profile;
        public Vector3 LookPoint => _lookPoint != null ? _lookPoint.position : transform.position;

        private void OnEnable()
        {
            InteractableRegistry.Register(this);
        }

        private void OnDisable()
        {
            InteractableRegistry.Unregister(this);
        }

        public bool CanInteract(in InteractionContext context)
        {
            return _canInteract && _profile != null;
        }

        public void BeginInteraction(in InteractionContext context, InteractionResponse response)
        {
            AILog.Log(AILog.INTERACTION, context.VillagerId, $"[Mock] {InteractableId} begin {response} ({context.Activity})");
        }

        public InteractionResult EndInteraction(in InteractionContext context, InteractionResponse response, InteractionEndReason reason)
        {
            bool detailed = response == InteractionResponse.Approach && reason == InteractionEndReason.Completed;
            float stress = detailed ? _profile.DetailedWitnessStress : _profile.WitnessStress;
            AILog.Log(AILog.INTERACTION, context.VillagerId, $"[Mock] {InteractableId} end {response} {reason}");
            return new InteractionResult(InteractableId, _profile.Kind, response, true, detailed, stress);
        }

        [ContextMenu("Renew Id (villagers react again)")]
        private void RenewId()
        {
            _generation++;
            AILog.Log(AILog.INTERACTION, $"[Mock] {name} renewed as {InteractableId}");
        }

        private void OnDrawGizmosSelected()
        {
            if (_profile == null || !_profile.HasNoticeRadius)
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, _profile.NoticeRadius);
        }
    }
}
