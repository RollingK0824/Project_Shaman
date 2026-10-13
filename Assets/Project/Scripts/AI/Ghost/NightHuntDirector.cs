using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Ghost
{
    public class NightHuntDirector : MonoBehaviour
    {
        [SerializeField] private AIFactory _factory;
        [SerializeField] private AIManager _manager;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;
        [SerializeField] private GameObject _ghostEntityPrefab;
        [SerializeField] private List<GhostNightActionData> _nightActions = new List<GhostNightActionData>();

        [Header("Runtime")]
        [SerializeField] private string _huntTokenGhostId;
        [SerializeField] private int _activeEntityCount;

        private struct PendingEntity
        {
            public AI_Possession Possession;
            public AI_Core Host;
            public float Radius;
            public bool IsHunter;
        }

        private readonly List<AI_GhostEntity> _entities = new List<AI_GhostEntity>();
        private readonly List<PendingEntity> _pendingEntities = new List<PendingEntity>();
        private TimeManager _time;
        private NightOutcomeLedger _ledger;
        private float _pendingSpawnTime = -1f;

        public IReadOnlyList<GhostNightActionData> NightActions => _nightActions;

        private void Awake()
        {
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
            _time = TimeManager.Instance;

            if (_time == null || _manager == null || _behaviourConfig == null || _ghostEntityPrefab == null)
            {
                AILog.Error(AILog.GHOST, "NightHuntDirector missing references (TimeManager, manager, config or ghost prefab)");
                return;
            }

            _ledger = new NightOutcomeLedger(_manager, _factory.SpawnHandler, _behaviourConfig);

            if (_nightActions.Count == 0)
            {
                AILog.Warn(AILog.GHOST, "NightHuntDirector has no night actions, ghosts will idle at night");
            }

            UnsubscribeTime();
            _time.OnDayStart += HandleDayStart;
            _time.OnNightStart += HandleNightStart;
        }

        private void HandleVillagersCleared()
        {
            UnsubscribeTime();
            _time = null;
            _ledger?.Clear();
            _ledger = null;
            _entities.Clear();
            _pendingEntities.Clear();
            _pendingSpawnTime = -1f;
            _activeEntityCount = 0;
        }

        private void UnsubscribeTime()
        {
            if (_time != null)
            {
                _time.OnDayStart -= HandleDayStart;
                _time.OnNightStart -= HandleNightStart;
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

            _ledger.BeginNight(_time.DayCount);
            AILog.Log(AILog.GHOST, $"Ghost targets: {GhostTargetRegistry.Count} (barriers {GhostBarrierRegistry.Count})");

            AIManager.VillagerRuntime hunterHost = PickHuntToken(hosts);
            _huntTokenGhostId = hunterHost.Profile.Possession.GhostId;
            _pendingEntities.Clear();

            foreach (AIManager.VillagerRuntime host in hosts)
            {
                AI_Possession possession = host.Profile.Possession;
                bool isHunter = host == hunterHost;
                float yinBefore = possession.Yin;
                float radius = Mathf.Lerp(_behaviourConfig.HuntRadiusMin, _behaviourConfig.HuntRadiusMax, CorruptionPlanner.GetYinFactor(yinBefore, _behaviourConfig));

                if (isHunter)
                {
                    possession.SpendYin(_behaviourConfig.HuntCost, "hunt token (pre-paid, no refund)");
                    AILog.Log(AILog.GHOST, possession.GhostId, $"Hunt token Day{_time.DayCount} (yin {yinBefore:F0} → {possession.Yin:F0}, radius {radius:F1}m)");
                }
                else
                {
                    AILog.Log(AILog.GHOST, possession.GhostId, $"No hunt token Day{_time.DayCount}, accumulation/interference only (yin {yinBefore:F0}, radius {radius:F1}m)");
                }

                _pendingEntities.Add(new PendingEntity { Possession = possession, Host = host.Core, Radius = radius, IsHunter = isHunter });
            }

            _pendingSpawnTime = Time.time + _behaviourConfig.GhostSpawnDelaySeconds;
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

            System.Random random = new System.Random(unchecked(_factory.Seed * 31 + _time.DayCount));
            return best[random.Next(best.Count)];
        }

        private void Update()
        {
            if (_pendingSpawnTime < 0f || Time.time < _pendingSpawnTime)
            {
                return;
            }

            _pendingSpawnTime = -1f;

            foreach (PendingEntity pending in _pendingEntities)
            {
                SpawnEntity(pending);
            }

            _pendingEntities.Clear();
        }

        private void SpawnEntity(PendingEntity pending)
        {
            if (pending.Possession == null || pending.Host == null || _factory.SpawnHandler == null)
            {
                return;
            }

            Vector3 center = pending.Host.Profile.HomePosition;
            GameObject instance = Instantiate(_ghostEntityPrefab, center, Quaternion.identity);
            instance.name = $"GhostEntity_{pending.Possession.GhostId}";

            AI_GhostEntity entity = instance.GetComponent<AI_GhostEntity>();
            if (entity == null)
            {
                AILog.Error(AILog.GHOST, $"{_ghostEntityPrefab.name} has no AI_GhostEntity");
                Destroy(instance);
                return;
            }

            int seed = unchecked(pending.Possession.Seed * 397 + _time.DayCount);
            entity.Initialize(pending.Possession, _ledger, _nightActions, pending.Host.LogId, center, pending.Radius, pending.IsHunter, _behaviourConfig, seed);
            _factory.SpawnHandler.Spawn(instance);
            _entities.Add(entity);
            _activeEntityCount = _entities.Count;
        }

        private void HandleDayStart()
        {
            _pendingSpawnTime = -1f;
            _pendingEntities.Clear();

            foreach (AI_GhostEntity entity in _entities)
            {
                if (entity != null)
                {
                    entity.ReleaseForDespawn();
                    AILog.Log(AILog.GHOST, entity.GhostId, "Morning: entity vanishes");
                    _factory.SpawnHandler.Despawn(entity.gameObject);
                }
            }

            _entities.Clear();
            _activeEntityCount = 0;
            _huntTokenGhostId = string.Empty;
            _ledger?.FlushMorning(_time.DayCount);
        }
    }
}
