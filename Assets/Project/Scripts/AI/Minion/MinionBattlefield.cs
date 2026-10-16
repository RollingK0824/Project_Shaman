using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Minion
{
    public readonly struct MinionPlayer
    {
        public readonly IGhostTarget Target;
        public readonly IDamageable Damageable;

        public MinionPlayer(IGhostTarget target, IDamageable damageable)
        {
            Target = target;
            Damageable = damageable;
        }

        public bool IsValid => Target != null
            && !(Target is Object unityObject && unityObject == null)
            && Damageable != null
            && !Damageable.IsDead;

        public Vector3 Position => Target.gameObject.transform.position;
        public string Name => Target.gameObject.name;
    }

    public class MinionBattlefield
    {
        private const float PLAYER_REFRESH_INTERVAL = 0.2f;

        private readonly List<IGhostTarget> _registryBuffer = new List<IGhostTarget>();
        private readonly List<MinionPlayer> _players = new List<MinionPlayer>();
        private float _nextRefreshTime = float.NegativeInfinity;

        public Transform Objective { get; }
        public IDamageable ObjectiveDamageable { get; }
        public ISpawnHandler SpawnHandler { get; }
        public IReadOnlyList<MinionPlayer> Players => _players;

        public MinionBattlefield(Transform objective, IDamageable objectiveDamageable, ISpawnHandler spawnHandler)
        {
            Objective = objective;
            ObjectiveDamageable = objectiveDamageable;
            SpawnHandler = spawnHandler;
        }

        public void RefreshPlayersIfDue()
        {
            if (Time.time >= _nextRefreshTime)
            {
                RefreshPlayers();
            }
        }

        public void RefreshPlayers()
        {
            _nextRefreshTime = Time.time + PLAYER_REFRESH_INTERVAL;
            _players.Clear();

            GhostTargetRegistry.CollectOfKind(GhostTargetKind.Player, _registryBuffer);

            foreach (IGhostTarget target in _registryBuffer)
            {
                MinionPlayer player = new MinionPlayer(target, target.gameObject.GetComponentInParent<IDamageable>());
                if (player.IsValid)
                {
                    _players.Add(player);
                }
            }

            _registryBuffer.Clear();
        }

        public bool TryFindNearestPlayer(Vector3 from, float radius, out MinionPlayer nearest)
        {
            nearest = default;
            float bestSqrDistance = radius * radius;
            bool found = false;

            foreach (MinionPlayer player in _players)
            {
                if (!player.IsValid)
                {
                    continue;
                }

                float sqrDistance = FlatSqrDistance(from, player.Position);
                if (sqrDistance <= bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    nearest = player;
                    found = true;
                }
            }

            return found;
        }

        public bool IsNearAnyPlayer(Vector3 position, float distance)
        {
            if (distance <= 0f)
            {
                return false;
            }

            float sqrDistance = distance * distance;

            foreach (MinionPlayer player in _players)
            {
                if (player.IsValid && FlatSqrDistance(position, player.Position) < sqrDistance)
                {
                    return true;
                }
            }

            return false;
        }

        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            return Mathf.Sqrt(FlatSqrDistance(a, b));
        }

        private static float FlatSqrDistance(Vector3 a, Vector3 b)
        {
            Vector3 offset = a - b;
            offset.y = 0f;
            return offset.sqrMagnitude;
        }
    }
}
