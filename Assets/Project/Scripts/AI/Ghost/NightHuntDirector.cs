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
            public int HuntKills;
            public bool HuntIgnoresRadius;
            public bool ChasePlayer;
        }

        private readonly List<AI_GhostEntity> _entities = new List<AI_GhostEntity>();
        private readonly List<PendingEntity> _pendingEntities = new List<PendingEntity>();
        private readonly HashSet<string> _excludedGhostIds = new HashSet<string>();
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
            _excludedGhostIds.Clear();
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

            _pendingEntities.Clear();

            foreach (AIManager.VillagerRuntime host in hosts)
            {
                AI_Possession possession = host.Profile.Possession;

                if (_excludedGhostIds.Contains(possession.GhostId))
                {
                    AILog.Log(AILog.GHOST, possession.GhostId, $"Night Day{_time.DayCount}: exorcism target, no entity tonight");
                    continue;
                }

                float radius = Mathf.Lerp(_behaviourConfig.HuntRadiusMin, _behaviourConfig.HuntRadiusMax, CorruptionPlanner.GetYinFactor(possession.Yin, _behaviourConfig));
                AILog.Log(AILog.GHOST, possession.GhostId, $"Night Day{_time.DayCount} (yin {possession.Yin:F0}, radius {radius:F1}m)");
                _pendingEntities.Add(new PendingEntity { Possession = possession, Host = host.Core, Radius = radius });
            }

            _pendingSpawnTime = Time.time + _behaviourConfig.GhostSpawnDelaySeconds;
        }

        public void ExcludeGhostTonight(string ghostId)
        {
            if (string.IsNullOrEmpty(ghostId) || !_excludedGhostIds.Add(ghostId))
            {
                return;
            }

            int removed = _pendingEntities.RemoveAll(pending => pending.Possession != null && pending.Possession.GhostId == ghostId);

            for (int i = _entities.Count - 1; i >= 0; i--)
            {
                AI_GhostEntity entity = _entities[i];
                if (entity == null || entity.GhostId != ghostId)
                {
                    continue;
                }

                entity.ReleaseForDespawn();
                _factory.SpawnHandler?.Despawn(entity.gameObject);
                _entities.RemoveAt(i);
                removed++;
            }

            _activeEntityCount = _entities.Count;
            AILog.Log(AILog.GHOST, ghostId, $"Excluded from hunt token and accumulation tonight (exorcism target, removed {removed})");
        }

        private void AssignHuntToken()
        {
            _huntTokenGhostId = string.Empty;

            if (!HasHuntableVillager(Vector3.zero, 0f, false))
            {
                AILog.Log(AILog.GHOST, $"No hunt Day{_time.DayCount}: no huntable villager left");
                return;
            }

            List<int> order = SortByYin();
            int hunterIndex = -1;

            foreach (int index in order)
            {
                PendingEntity candidate = _pendingEntities[index];
                if (HasHuntableVillager(candidate.Host.Profile.HomePosition, candidate.Radius, true))
                {
                    hunterIndex = index;
                    break;
                }

                AILog.Log(AILog.GHOST, candidate.Possession.GhostId, $"No villager in radius {candidate.Radius:F1}m, hunt token passes on");
            }

            bool ignoresRadius = hunterIndex < 0;
            if (ignoresRadius)
            {
                hunterIndex = order[0];
            }

            PendingEntity hunter = _pendingEntities[hunterIndex];
            float yinBefore = hunter.Possession.Yin;

            if (!_behaviourConfig.TryGetHuntTier(yinBefore, out HuntTier tier))
            {
                AILog.Log(AILog.GHOST, hunter.Possession.GhostId, $"No hunt Day{_time.DayCount}: yin {yinBefore:F0} below every hunt tier");
                return;
            }

            hunter.Possession.DrainYin($"hunt token Day{_time.DayCount}");
            hunter.HuntKills = tier.Kills;
            hunter.HuntIgnoresRadius = ignoresRadius;
            hunter.ChasePlayer = tier.ChasePlayer;
            _pendingEntities[hunterIndex] = hunter;
            _huntTokenGhostId = hunter.Possession.GhostId;

            AILog.Log(AILog.GHOST, _huntTokenGhostId, $"Hunt token Day{_time.DayCount} (yin {yinBefore:F0} → 0, tier {tier}{(ignoresRadius ? ", no villager in any radius → nearest villager" : string.Empty)})");
        }

        private List<int> SortByYin()
        {
            System.Random random = new System.Random(unchecked(_factory.Seed * 31 + _time.DayCount));
            List<int> order = new List<int>();
            List<double> tieBreakers = new List<double>();

            for (int i = 0; i < _pendingEntities.Count; i++)
            {
                order.Add(i);
                tieBreakers.Add(random.NextDouble());
            }

            order.Sort((a, b) =>
            {
                float yinA = _pendingEntities[a].Possession.Yin;
                float yinB = _pendingEntities[b].Possession.Yin;

                if (Mathf.Abs(yinA - yinB) > 0.001f)
                {
                    return yinB.CompareTo(yinA);
                }

                return tieBreakers[a].CompareTo(tieBreakers[b]);
            });

            return order;
        }

        private bool HasHuntableVillager(Vector3 center, float radius, bool checkRadius)
        {
            float sqrRadius = radius * radius;

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Core == null || villager.Profile == null || villager.Profile.IsPossessed)
                {
                    continue;
                }

                if (!checkRadius)
                {
                    return true;
                }

                Vector3 offset = villager.Core.transform.position - center;
                offset.y = 0f;

                if (offset.sqrMagnitude <= sqrRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            if (_pendingSpawnTime < 0f || Time.time < _pendingSpawnTime)
            {
                return;
            }

            _pendingSpawnTime = -1f;

            if (_pendingEntities.Count > 0)
            {
                AssignHuntToken();
            }

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
            entity.Initialize(pending.Possession, _ledger, _nightActions, pending.Host.LogId, center, pending.Radius, pending.HuntKills, pending.HuntIgnoresRadius, pending.ChasePlayer, _behaviourConfig, seed);
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
            _excludedGhostIds.Clear();
            _activeEntityCount = 0;
            _huntTokenGhostId = string.Empty;
            _ledger?.FlushMorning(_time.DayCount);
        }
    }
}
