using System.Collections.Generic;
using Mirror;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.World;

namespace ProjectShaman.Network.AI
{
    // A player can pick up a villager's tool (found for a FindTool request) and carry it to the giver.
    // Holding is judged on the server; clients only see whether the tool is held, to hide the prompt.
    [RequireComponent(typeof(NetworkIdentity), typeof(ToolItem))]
    public class Net_ToolItem : NetworkBehaviour, IInteractable
    {
        private const string CARRY_ANCHOR_NAME = "ToolCarryAnchor";
        private static readonly Vector3 CARRY_ANCHOR_LOCAL = new Vector3(0.35f, 1f, 0.35f);
        private static readonly List<Net_ToolItem> _carried = new List<Net_ToolItem>();

        [SerializeField] private float _maxPickUpDistance = 4f;

        private ToolItem _tool;
        private Collider _collider;
        private GameObject _carrier;
        private string _carrierId;

        [SyncVar(hook = nameof(OnHeldChanged))] private bool _isHeld;
        [SyncVar(hook = nameof(OnCarriedChanged))] private bool _isCarriedByPlayer;

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
            _collider = GetComponent<Collider>();
        }

        public override void OnStartServer()
        {
            if (TimeManager.Instance != null)
            {
                TimeManager.Instance.OnNightStart += HandleNightStart;
            }
        }

        public override void OnStopServer()
        {
            if (TimeManager.Existing != null)
            {
                TimeManager.Existing.OnNightStart -= HandleNightStart;
            }

            DropFromCarrier(transform.position, "tool despawned");
        }

        // Server: the tool this player is carrying, if any
        public static Net_ToolItem FindCarriedBy(GameObject player)
        {
            foreach (Net_ToolItem item in _carried)
            {
                if (item != null && item._carrier == player)
                {
                    return item;
                }
            }

            return null;
        }

        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && netId != 0 && !_isHeld && PlayerActionGuard.CanAct(interactor);
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

            if (FindCarriedBy(player) != null)
            {
                AILog.Log(AILog.TOOL, player.name, $"Already carrying a tool, {_tool.InstanceId} not picked up");
                return;
            }

            string carrierId = $"Player_{sender.identity.netId}";
            if (!_tool.TryPickUp(carrierId, CarryAnchor(player)))
            {
                AILog.Log(AILog.TOOL, player.name, $"{_tool.InstanceId} not available (held or reserved by {_tool.HolderId ?? _tool.ReservedBy})");
                return;
            }

            _carrier = player;
            _carrierId = carrierId;
            _carried.Add(this);
            _isCarriedByPlayer = true;
            ApplyCollider();
            AILog.Log(AILog.TOOL, carrierId, $"Picked up {_tool.InstanceId}({_tool.TypeId}) by player {player.name}");
        }

        // Server: the carrier lets go of the tool, on the ground at the position (or hands it on afterwards)
        [Server]
        public void DropFromCarrier(Vector3 position, string reason)
        {
            if (_carrier == null && string.IsNullOrEmpty(_carrierId))
            {
                return;
            }

            _tool.Drop(_carrierId, position, reason);
            _carried.Remove(this);
            _carrier = null;
            _carrierId = null;
            _isCarriedByPlayer = false;
            ApplyCollider();
        }

        [ServerCallback]
        private void Update()
        {
            _isHeld = _tool.IsHeld;

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

        // Requests end at night; a tool still carried is put down so the owner can find it again tomorrow
        private void HandleNightStart()
        {
            if (_carrier != null)
            {
                DropFromCarrier(_carrier.transform.position, "night started");
            }
        }

        private void OnHeldChanged(bool oldValue, bool newValue)
        {
            ApplyCollider();
        }

        private void OnCarriedChanged(bool oldValue, bool newValue)
        {
            ApplyCollider();
        }

        // A carried tool sits in front of the player and must not catch their interaction ray
        private void ApplyCollider()
        {
            if (_collider != null)
            {
                _collider.enabled = !_isCarriedByPlayer;
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
