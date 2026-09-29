using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Interfaces;

namespace ProjectShaman.AI.Ghost
{
    public class NightHuntDirector : MonoBehaviour
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private AIManager _manager;
        [SerializeField] private MonoBehaviour _timeSourceBehaviour;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;
        [SerializeField] private GameObject _ghostEntityPrefab;

        [Header("Runtime")]
        [SerializeField] private string _huntTokenGhostId;
        [SerializeField] private int _activeEntityCount;

        private readonly List<AI_GhostEntity> _entities = new List<AI_GhostEntity>();
        private ITimeSource _timeSource;
        private AI_Possession _pendingHunter;
        private AI_Core _pendingHost;
        private float _pendingRadius;
        private float _pendingSpawnTime = -1f;

        private void Awake()
        {
            _timeSource = _timeSourceBehaviour as ITimeSource;

            if (_factory != null)
            {
                _factory.OnVillagersCreated += HandleVillagersCreated;
                _factory.OnVillagersCleared += HandleVillagersCleared;
            }
        }

        private void OnDestroy()
        {
            if (_factory != null)
            {
                _factory.OnVillagersCreated -= HandleVillagersCreated;
                _factory.OnVillagersCleared -= HandleVillagersCleared;
            }

            UnsubscribeTime();
        }

        private void HandleVillagersCreated(IReadOnlyList<AI_Core> cores)
        {
            if (_timeSource == null || _manager == null || _behaviourConfig == null || _ghostEntityPrefab == null)
            {
                AILog.Error(AILog.GHOST, "NightHuntDirector missing references (time source, manager, config or ghost prefab)");
                return;
            }

            UnsubscribeTime();
            _timeSource.OnNightStart += HandleNightStart;
            _timeSource.OnNewDay += HandleNewDay;
        }

        private void HandleVillagersCleared()
        {
            UnsubscribeTime();
            _entities.Clear();
            _pendingSpawnTime = -1f;
            _activeEntityCount = 0;
        }

        private void UnsubscribeTime()
        {
            if (_timeSource != null)
            {
                _timeSource.OnNightStart -= HandleNightStart;
                _timeSource.OnNewDay -= HandleNewDay;
            }
        }

        private void HandleNightStart()
        {
            List<AIManager.VillagerRuntime> hosts = new List<AIManager.VillagerRuntime>();

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Profile.Possession != null)
                {
                    hosts.Add(villager);
                }
            }

            if (hosts.Count == 0)
            {
                AILog.Log(AILog.GHOST, "Night: no ghost alive");
                return;
            }

            AIManager.VillagerRuntime hunterHost = PickHuntToken(hosts);
            AI_Possession hunter = hunterHost.Profile.Possession;
            float yinBefore = hunter.Yin;

            _huntTokenGhostId = hunter.GhostId;
            _pendingHunter = hunter;
            _pendingHost = hunterHost.Core;
            _pendingRadius = Mathf.Lerp(_behaviourConfig.HuntRadiusMin, _behaviourConfig.HuntRadiusMax, CorruptionPlanner.GetYinFactor(yinBefore, _behaviourConfig));
            _pendingSpawnTime = Time.time + _behaviourConfig.GhostSpawnDelaySeconds;

            hunter.SpendYin(_behaviourConfig.HuntCost, "hunt token (pre-paid, no refund)");
            AILog.Log(AILog.GHOST, hunter.GhostId, $"Hunt token Day{_timeSource.DayCount} (yin {yinBefore:F0} → {hunter.Yin:F0}, radius {_pendingRadius:F1}m)");

            foreach (AIManager.VillagerRuntime host in hosts)
            {
                if (host != hunterHost)
                {
                    AILog.Log(AILog.STUB, host.Profile.Possession.GhostId, "No hunt token, accumulation/interference skipped");
                }
            }
        }

        private AIManager.VillagerRuntime PickHuntToken(List<AIManager.VillagerRuntime> hosts)
        {
            float bestYin = float.MinValue;
            List<AIManager.VillagerRuntime> best = new List<AIManager.VillagerRuntime>();

            foreach (AIManager.VillagerRuntime host in hosts)
            {
                float yin = host.Profile.Possession.Yin;
                if (yin > bestYin + 0.001f)
                {
                    bestYin = yin;
                    best.Clear();
                    best.Add(host);
                }
                else if (Mathf.Abs(yin - bestYin) <= 0.001f)
                {
                    best.Add(host);
                }
            }

            System.Random random = new System.Random(unchecked(_factory.Seed * 31 + _timeSource.DayCount));
            return best[random.Next(best.Count)];
        }

        private void Update()
        {
            if (_pendingSpawnTime < 0f || Time.time < _pendingSpawnTime)
            {
                return;
            }

            _pendingSpawnTime = -1f;
            SpawnEntity();
        }

        private void SpawnEntity()
        {
            if (_pendingHunter == null || _pendingHost == null || _factory.SpawnHandler == null)
            {
                return;
            }

            Vector3 center = _pendingHost.Profile.HomePosition;
            GameObject instance = Instantiate(_ghostEntityPrefab, center, Quaternion.identity);
            instance.name = $"GhostEntity_{_pendingHunter.GhostId}";

            AI_GhostEntity entity = instance.GetComponent<AI_GhostEntity>();
            if (entity == null)
            {
                AILog.Error(AILog.GHOST, $"{_ghostEntityPrefab.name} has no AI_GhostEntity");
                Destroy(instance);
                return;
            }

            entity.Initialize(this, _pendingHunter.GhostId, _pendingHost.LogId, center, _pendingRadius, _behaviourConfig.GhostKillsPerHunt, _behaviourConfig.GhostKillDistance);
            _factory.SpawnHandler.Spawn(instance);
            _entities.Add(entity);
            _activeEntityCount = _entities.Count;
        }

        private void HandleNewDay(int dayCount)
        {
            _pendingSpawnTime = -1f;

            foreach (AI_GhostEntity entity in _entities)
            {
                if (entity != null)
                {
                    AILog.Log(AILog.GHOST, entity.GhostId, "Morning: entity vanishes");
                    _factory.SpawnHandler.Despawn(entity.gameObject);
                }
            }

            _entities.Clear();
            _activeEntityCount = 0;
        }

        public bool IsHuntable(AI_Core core)
        {
            return core != null && core.Profile != null && !core.Profile.IsPossessed && IsAlive(core);
        }

        private bool IsAlive(AI_Core core)
        {
            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Core == core)
                {
                    return true;
                }
            }

            return false;
        }

        public AI_Core FindHuntTarget(AI_GhostEntity entity)
        {
            AI_Core best = null;
            float bestDistance = float.MaxValue;

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                AI_Core core = villager.Core;
                if (!IsHuntable(core) || !entity.IsInsideRadius(core.transform.position))
                {
                    continue;
                }

                float distance = (core.transform.position - entity.transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = core;
                }
            }

            return best;
        }

        public void Kill(AI_GhostEntity entity, AI_Core victim)
        {
            AILog.Log(AILog.GHOST, entity.GhostId, $"Killed {victim.LogId}");
            _manager.RemoveVillager(victim, $"killed by {entity.GhostId}");
        }
    }
}
