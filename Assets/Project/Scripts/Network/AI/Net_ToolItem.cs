using System.Collections.Generic;
using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.World;

namespace ProjectShaman.Network.AI
{
    // A player can pick up a villager's tool (found for a FindTool request) and carry it to the giver.
    // Holding is judged on the server; clients only see whether the tool is held or claimed, to hide the prompt.
    [RequireComponent(typeof(NetworkIdentity), typeof(ToolItem))]
    public class Net_ToolItem : NetworkBehaviour, IInteractable
    {
        private const string CARRY_ANCHOR_NAME = "ToolCarryAnchor";
        private static readonly Vector3 CARRY_ANCHOR_LOCAL = new Vector3(0.35f, 1f, 0.35f);
        private static readonly List<Net_ToolItem> _carried = new List<Net_ToolItem>();

        [SerializeField] private float _maxPickUpDistance = 4f;

        private ToolItem _tool;
        private GameObject _carrier;
        private string _carrierId;
        private ItemData _stowedItem;
        private PlayerInventory _carrierInventory;

        [SyncVar(hook = nameof(OnHeldChanged))] private bool _isHeld;
        [SyncVar(hook = nameof(OnStowedChanged))] private bool _isStowed;
        [SyncVar] private bool _isUnclaimed;

        public ToolItem Tool => _tool;
        public GameObject Carrier => _carrier;
        public string InteractionPrompt => $"{AIDisplayNames.Tool(_tool.TypeId)} 줍기";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _carried.Clear();
        }

        private void Awake()
        {
            _tool = GetComponent<ToolItem>();
        }

        // SyncVar hooks do not run for the state a late joiner receives on spawn
        public override void OnStartClient()
        {
            ApplyHeldOnClient();
            ApplyStowedOnClient();
        }

        // A tool a player carries stays with them through the night: a find-tool request runs until a later day (C120)
        public override void OnStopServer()
        {
            DropFromCarrier(transform.position, "tool despawned");
        }

        // Server: a tool this player is carrying (of the kind, when given), if any
        public static Net_ToolItem FindCarriedBy(GameObject player, string typeId = null)
        {
            foreach (Net_ToolItem item in _carried)
            {
                if (item != null && item._carrier == player && (typeId == null || item._tool.TypeId == typeId))
                {
                    return item;
                }
            }

            return null;
        }

        // Server: this player has a tool of that item in the inventory (dropping it puts that tool down)
        public static bool IsStowedBy(GameObject player, ItemData itemData)
        {
            return itemData != null && CountStowed(player, itemData) > 0;
        }

        private static int CountStowed(GameObject player, ItemData itemData)
        {
            int count = 0;
            foreach (Net_ToolItem item in _carried)
            {
                if (item != null && item._carrier == player && item._stowedItem == itemData)
                {
                    count++;
                }
            }

            return count;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && netId != 0 && _isUnclaimed && PlayerActionGuard.CanAct(interactor);
        }

        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            CmdRequestPickUp();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestPickUp(NetworkConnectionToClient sender = null)
        {
            GameObject player = sender != null && sender.identity != null ? sender.identity.gameObject : null;
            if (player == null)
            {
                AILog.Warn(AILog.NET, $"[{name}] Pick up request without player object, ignored");
                return;
            }

            if ((player.transform.position - transform.position).sqrMagnitude > _maxPickUpDistance * _maxPickUpDistance)
            {
                AILog.Warn(AILog.NET, $"[{name}] Pick up request from {player.name} too far, ignored");
                return;
            }

            if (_tool.ItemData == null && FindCarriedBy(player) != null)
            {
                AILog.Log(AILog.TOOL, player.name, $"Already carrying a tool, {_tool.InstanceId} has no item data and is not picked up");
                return;
            }

            if (!_tool.IsUnclaimed)
            {
                AILog.Log(AILog.TOOL, player.name, $"{_tool.InstanceId} not available (held or reserved by {_tool.HolderId ?? _tool.ReservedBy})");
                return;
            }

            ItemData item = _tool.ItemData;
            PlayerInventory inventory = item != null ? player.GetComponent<PlayerInventory>() : null;
            if (item != null && (inventory == null || !inventory.TryAddItem(item)))
            {
                AILog.Log(AILog.TOOL, player.name, $"{_tool.InstanceId} not picked up, inventory refused {item.DisplayName}");
                return;
            }

            string carrierId = $"Player_{sender.identity.netId}";
            if (!_tool.TryPickUp(carrierId, CarryAnchor(player)))
            {
                if (inventory != null)
                {
                    RemoveFromInventory(player, inventory, item);
                }

                AILog.Log(AILog.TOOL, player.name, $"{_tool.InstanceId} not available (held or reserved by {_tool.HolderId ?? _tool.ReservedBy})");
                return;
            }

            _carrier = player;
            _carrierId = carrierId;
            _carried.Add(this);

            if (inventory != null)
            {
                _stowedItem = item;
                _carrierInventory = inventory;
                _carrierInventory.ItemUnitsChanged += HandleCarrierItemUnitsChanged;
                _isStowed = true;
                _tool.ApplyStowedState(true);
            }

            AILog.Log(AILog.TOOL, carrierId, $"Picked up {_tool.InstanceId}({_tool.TypeId}) by player {player.name}{(inventory != null ? ", into inventory" : string.Empty)}");
        }

        // Server: the carrier lets go of the tool, on the ground at the position (or hands it on afterwards)
        [Server]
        public void DropFromCarrier(Vector3 position, string reason)
        {
            if (_carrier == null && string.IsNullOrEmpty(_carrierId))
            {
                return;
            }

            Unstow();
            _tool.Drop(_carrierId, position, reason);
            _carried.Remove(this);
            _carrier = null;
            _carrierId = null;
        }

        [Server]
        private void Unstow()
        {
            if (_stowedItem == null)
            {
                return;
            }

            ItemData item = _stowedItem;
            PlayerInventory inventory = _carrierInventory;
            _stowedItem = null;
            _carrierInventory = null;
            _isStowed = false;
            _tool.ApplyStowedState(false);

            if (inventory == null)
            {
                return;
            }

            inventory.ItemUnitsChanged -= HandleCarrierItemUnitsChanged;
            if (inventory.GetItemCount(item) > CountStowed(inventory.gameObject, item))
            {
                RemoveFromInventory(inventory.gameObject, inventory, item);
            }
        }

        [Server]
        private void RemoveFromInventory(GameObject player, PlayerInventory inventory, ItemData item)
        {
            bool removed = (player.TryGetComponent(out NetPlayerInventory netInventory) && netInventory.ServerConsume(item))
                || inventory.RemoveItem(item);

            if (!removed)
            {
                AILog.Warn(AILog.TOOL, $"{item.DisplayName} could not be removed from the inventory of {player.name}");
            }
        }

        private void HandleCarrierItemUnitsChanged(ItemData item, int delta)
        {
            if (item != _stowedItem || delta >= 0 || _carrierInventory.GetItemCount(item) >= CountStowed(_carrier, item))
            {
                return;
            }

            DropFromCarrier(_carrier != null ? _carrier.transform.position : transform.position, "left the carrier's inventory");
        }

        [ServerCallback]
        private void Update()
        {
            _isHeld = _tool.IsHeld;
            _isUnclaimed = _tool.IsUnclaimed;

            if (string.IsNullOrEmpty(_carrierId))
            {
                return;
            }

            // A carrier who left or died lets go where they stood
            if (_carrier == null)
            {
                DropFromCarrier(transform.position, "carrier left");
            }
            else if (_carrier.TryGetComponent(out PlayerHealth health) && health.IsDead)
            {
                DropFromCarrier(_carrier.transform.position, "carrier died");
            }
        }

        private void OnHeldChanged(bool oldValue, bool newValue)
        {
            ApplyHeldOnClient();
        }

        // The server's ToolItem already applied it on pick up and drop
        private void ApplyHeldOnClient()
        {
            if (!isServer)
            {
                _tool.ApplyHeldState(_isHeld);
            }
        }

        private void OnStowedChanged(bool oldValue, bool newValue)
        {
            ApplyStowedOnClient();
        }

        private void ApplyStowedOnClient()
        {
            if (!isServer)
            {
                _tool.ApplyStowedState(_isStowed);
            }
        }

        private static Transform CarryAnchor(GameObject player)
        {
            Transform anchor = player.transform.Find(CARRY_ANCHOR_NAME);
            if (anchor == null)
            {
                anchor = new GameObject(CARRY_ANCHOR_NAME).transform;
                anchor.SetParent(player.transform, false);
                anchor.localPosition = CARRY_ANCHOR_LOCAL;
            }

            return anchor;
        }
    }
}
