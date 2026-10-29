using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Interaction;

namespace ProjectShaman.AI.World
{
    public enum ToolHolderMode
    {
        Hang,
        Lean
    }

    public class ToolHolder : MonoBehaviour, IVillagerInteractable
    {
        private const float LOOK_HEIGHT = 1f;

        private static InteractionProfile _putAwayProfile;

        [SerializeField] private string _holderId;
        [Tooltip("Hang: the tool is held exactly at the slot. Lean: stood at the slot and left to tip against the wall")]
        [SerializeField] private ToolHolderMode _mode = ToolHolderMode.Hang;
        [Tooltip("Filled in order. Villagers walk to this object's position, so keep it on the floor in front of the slots")]
        [SerializeField] private List<Transform> _slots = new List<Transform>();
        [Tooltip("Empty = every tool")]
        [SerializeField] private List<string> _acceptedToolTypes = new List<string>();

        [Header("Runtime")]
        [SerializeField] private List<string> _occupantIds = new List<string>();

        public string HolderId => string.IsNullOrEmpty(_holderId) ? name : _holderId;
        public ToolHolderMode Mode => _mode;
        public int SlotCount => _slots.Count;

        public string InteractableId => $"ToolHolder_{HolderId}";
        public InteractionProfile Profile => _putAwayProfile != null ? _putAwayProfile : (_putAwayProfile = CreatePutAwayProfile());
        public Vector3 LookPoint => SlotCenter() + Vector3.up * (_slots.Count > 0 ? 0f : LOOK_HEIGHT);

        public bool Accepts(string toolTypeId)
        {
            return _acceptedToolTypes.Count == 0 || (!string.IsNullOrEmpty(toolTypeId) && _acceptedToolTypes.Contains(toolTypeId));
        }

        public bool TryReserve(ToolItem tool, out int slot)
        {
            slot = -1;
            if (tool == null || !Accepts(tool.TypeId))
            {
                return false;
            }

            EnsureOccupants();
            int existing = _occupantIds.IndexOf(tool.InstanceId);
            if (existing >= 0)
            {
                slot = existing;
                return true;
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i] != null && string.IsNullOrEmpty(_occupantIds[i]))
                {
                    _occupantIds[i] = tool.InstanceId;
                    slot = i;
                    AILog.Log(AILog.TOOL, tool.InstanceId, $"Holder {HolderId} slot {i} reserved ({_mode})");
                    return true;
                }
            }

            return false;
        }

        public void Release(ToolItem tool)
        {
            if (tool == null)
            {
                return;
            }

            int slot = _occupantIds.IndexOf(tool.InstanceId);
            if (slot < 0)
            {
                return;
            }

            _occupantIds[slot] = string.Empty;
            AILog.Log(AILog.TOOL, tool.InstanceId, $"Holder {HolderId} slot {slot} released");
        }

        public Transform GetSlot(int slot)
        {
            return slot >= 0 && slot < _slots.Count ? _slots[slot] : null;
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

        private Vector3 SlotCenter()
        {
            Vector3 sum = Vector3.zero;
            int count = 0;
            foreach (Transform slot in _slots)
            {
                if (slot != null)
                {
                    sum += slot.position;
                    count++;
                }
            }

            return count > 0 ? sum / count : transform.position;
        }

        private void EnsureOccupants()
        {
            while (_occupantIds.Count < _slots.Count)
            {
                _occupantIds.Add(string.Empty);
            }
        }

        private static InteractionProfile CreatePutAwayProfile()
        {
            InteractionProfile profile = ScriptableObject.CreateInstance<InteractionProfile>();
            profile.name = "ToolPutAway";
            profile.hideFlags = HideFlags.DontSave;
            return profile;
        }

        private void OnDrawGizmos()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                Transform slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                bool occupied = i < _occupantIds.Count && !string.IsNullOrEmpty(_occupantIds[i]);
                Gizmos.color = occupied ? Color.red : (_mode == ToolHolderMode.Hang ? Color.green : new Color(0.6f, 1f, 0.3f));
                Gizmos.DrawLine(transform.position, slot.position);
                Gizmos.matrix = slot.localToWorldMatrix;
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(0.1f, 0.6f, 0.1f));
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}
