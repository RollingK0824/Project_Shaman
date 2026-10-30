using System.Collections.Generic;
using Mirror;
using UnityEngine;
using ProjectShaman.AI;

namespace ProjectShaman.Network.AI
{
    // Server: a player walking into this villager bumps it (AI_Bump.Shove). Every peer: the body tilt (RPC).
    // A player standing next to the villager does not bump it: only one moving toward it
    [RequireComponent(typeof(NetworkIdentity), typeof(AI_Bump))]
    public class Net_VillagerBump : NetworkBehaviour
    {
        [SerializeField, Min(0.02f)] private float _checkInterval = 0.1f;
        [Tooltip("Ground distance between centres that counts as touching (player capsule 0.44 + villager 0.25 + a little)")]
        [SerializeField, Min(0.3f)] private float _contactDistance = 0.85f;
        [Tooltip("Speed toward the villager (m/s) a player needs for a bump")]
        [SerializeField, Min(0f)] private float _minApproachSpeed = 0.8f;
        [Tooltip("Share of the player's heading in the shove; the rest pushes the villager off the player's line")]
        [SerializeField, Range(0f, 1f)] private float _forwardShare = 0.3f;

        private AI_Bump _bump;
        private float _nextCheckTime;
        private readonly Dictionary<uint, Vector3> _lastPlayerPositions = new Dictionary<uint, Vector3>();
        private readonly List<uint> _seen = new List<uint>();

        private void Awake()
        {
            _bump = GetComponent<AI_Bump>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _bump.IsSimulating = true;
            _bump.OnBumped += HandleServerBumped;
        }

        public override void OnStopServer()
        {
            _bump.OnBumped -= HandleServerBumped;
            _bump.IsSimulating = false;
            base.OnStopServer();
        }

        [ServerCallback]
        private void Update()
        {
            if (Time.time < _nextCheckTime)
            {
                return;
            }

            float elapsed = _checkInterval + (Time.time - _nextCheckTime);
            _nextCheckTime = Time.time + _checkInterval;
            Vector3 position = transform.position;
            _seen.Clear();

            foreach (NetworkConnectionToClient connection in NetworkServer.connections.Values)
            {
                NetworkIdentity player = connection != null ? connection.identity : null;
                if (player == null)
                {
                    continue;
                }

                uint id = player.netId;
                Vector3 now = player.transform.position;
                _seen.Add(id);
                bool hadLast = _lastPlayerPositions.TryGetValue(id, out Vector3 last);
                _lastPlayerPositions[id] = now;
                if (!hadLast)
                {
                    continue;
                }

                Vector3 offset = position - now;
                offset.y = 0f;
                Vector3 velocity = (now - last) / Mathf.Max(0.01f, elapsed);
                velocity.y = 0f;
                Vector3 toVillager = offset.sqrMagnitude > 0.0001f ? offset.normalized : player.transform.forward;

                // Either one walking into the other: the player into the villager, or the villager into the player
                // (a villager that walks into someone stops, so it no longer shoves the player along)
                Vector3 villagerVelocity = _bump.Velocity;
                villagerVelocity.y = 0f;
                float approach = Vector3.Dot(velocity - villagerVelocity, toVillager);

                // Looked ahead by one check, so a fast walker is caught before it is already inside the player
                float reach = _contactDistance + Mathf.Max(0f, approach) * _checkInterval;
                if (offset.sqrMagnitude > reach * reach || approach < _minApproachSpeed)
                {
                    continue;
                }

                // Off the mover's line (to the side the villager already stands on), with a little of the heading
                Vector3 moving = velocity.sqrMagnitude >= villagerVelocity.sqrMagnitude ? velocity : -villagerVelocity;
                Vector3 heading = moving.sqrMagnitude > 0.0001f ? moving.normalized : toVillager;
                Vector3 side = offset - Vector3.Dot(offset, heading) * heading;
                if (side.sqrMagnitude < 0.0001f)
                {
                    side = Vector3.Cross(Vector3.up, heading);
                }

                Vector3 direction = side.normalized * (1f - _forwardShare) + heading * _forwardShare;
                _bump.Shove(direction, now, player.name, true);
            }

            if (_lastPlayerPositions.Count > _seen.Count)
            {
                ForgetGonePlayers();
            }
        }

        private void ForgetGonePlayers()
        {
            List<uint> gone = new List<uint>();
            foreach (uint id in _lastPlayerPositions.Keys)
            {
                if (!_seen.Contains(id))
                {
                    gone.Add(id);
                }
            }

            foreach (uint id in gone)
            {
                _lastPlayerPositions.Remove(id);
            }
        }

        private void HandleServerBumped(Vector3 direction, Vector3 bumperPosition)
        {
            RpcBumped(direction, bumperPosition);
        }

        [ClientRpc]
        private void RpcBumped(Vector3 direction, Vector3 bumperPosition)
        {
            _bump.PlayTilt(direction, bumperPosition);
        }
    }
}
