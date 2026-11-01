using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
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
            public AIManager.VillagerRuntime HostRuntime;
            public float Radius;
            public int HuntKills;
            public bool HuntIgnoresRadius;
            public bool ChasePlayer;
            // Yin when the night began, before a hunt token spends it: full yin = the ghost comes out awakened (warning scream)
            public float YinAtNightStart;
        }

        private readonly List<AI_GhostEntity> _entities = new List<AI_GhostEntity>();
        private readonly List<PendingEntity> _pendingEntities = new List<PendingEntity>();
        private readonly HashSet<string> _excludedGhostIds = new HashSet<string>();
        private TimeManager _time;
        private NightOutcomeLedger _ledger;
        private const float SIGHTING_EYE_OFFSET = 1f;
        // The ghost steps out beside its sleeping host, not inside it: spawned on the host it was stuck in the host's body
        private const float SPAWN_BESIDE_HOST = 1.5f;
        private const float SPAWN_CLEARANCE = 0.6f;
        private const int SPAWN_TRIES = 8;

        private float _pendingSpawnTime = -1f;
        private bool _isHuntTokenAssigned;
        private float _nextSightingScanTime;

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
            // Where anomalies may be left: worked out from the houses once, nothing placed in the scene
            ProjectShaman.AI.World.AnomalySpotPlanner.Build();

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
                _pendingEntities.Add(new PendingEntity { Possession = possession, Host = host.Core, HostRuntime = host, Radius = radius, YinAtNightStart = possession.Yin });
            }

            _pendingSpawnTime = Time.time + _behaviourConfig.GhostSpawnDelaySeconds;
            _isHuntTokenAssigned = false;
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
            SpawnSleepingHosts();
            ScanSightings();
        }

        // A villager still out at night who sees a ghost remembers where (C122). No other effect: the stress is the cost,
        // a death mark it may bring is the stress's doing. Villagers in their houses see nothing
        private void ScanSightings()
        {
            if (_entities.Count == 0 || Time.time < _nextSightingScanTime)
            {
                return;
            }

            _nextSightingScanTime = Time.time + _behaviourConfig.ObservationInterval;
            float radiusSqr = _behaviourConfig.GhostSightingRadius * _behaviourConfig.GhostSightingRadius;

            foreach (AIManager.VillagerRuntime villager in _manager.Villagers)
            {
                if (villager.Core == null || villager.Memory == null || villager.Perception == null || villager.HasArrivedHome
                    || villager.Profile.IsPossessed || !_ledger.IsAlive(villager.Core))
                {
                    continue;
                }

                Vector3 from = villager.Core.transform.position;

                foreach (AI_GhostEntity entity in _entities)
                {
                    if (entity == null)
                    {
                        continue;
                    }

                    Vector3 position = entity.transform.position;
                    if ((position - from).sqrMagnitude > radiusSqr || !villager.Perception.CanSee(position + Vector3.up * SIGHTING_EYE_OFFSET))
                    {
                        continue;
                    }

                    if (villager.Memory.RecordGhostSighting(entity.HostVillagerId, position))
                    {
                        AILog.Log(AILog.GHOST, entity.GhostId, $"Seen by {villager.Id} on the way home");
                        villager.Conversation?.Say("귀, 귀신이다!", "saw a ghost");
                        AIEvents.RaiseStress(villager.Id, $"saw ghost {entity.GhostId}", _behaviourConfig.GhostSightingStress);
                    }
                }
            }
        }

        private void SpawnSleepingHosts()
        {
            if (_pendingSpawnTime < 0f || Time.time < _pendingSpawnTime)
            {
                return;
            }

            // The token is drawn once for the night among every ghost, before any has left its host
            if (!_isHuntTokenAssigned)
            {
                _isHuntTokenAssigned = true;
                if (_pendingEntities.Count > 0)
                {
                    AssignHuntToken();
                }
            }

            // A ghost leaves its host while the host sleeps at home (C121): a host held up on the way home
            // brings its ghost out late, and no host is treated differently on the way
            for (int i = _pendingEntities.Count - 1; i >= 0; i--)
            {
                PendingEntity pending = _pendingEntities[i];
                if (!_manager.IsLyingInBed(pending.HostRuntime))
                {
                    continue;
                }

                _pendingEntities.RemoveAt(i);
                AILog.Log(AILog.GHOST, pending.Possession.GhostId, $"{pending.Host.LogId} lay down in bed, the ghost comes out");
                SpawnEntity(pending);
            }

            if (_pendingEntities.Count == 0)
            {
                _pendingSpawnTime = -1f;
            }
        }

        private void SpawnEntity(PendingEntity pending)
        {
            if (pending.Possession == null || pending.Host == null || _factory.SpawnHandler == null)
            {
                return;
            }

            Vector3 center = pending.Host.Profile.HomePosition;
            GameObject instance = Instantiate(_ghostEntityPrefab, PickSpawnPoint(pending.Host.transform.position, center), Quaternion.identity);
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
            entity.SetAwakened(pending.YinAtNightStart >= _behaviourConfig.MaxYin);
            // Model list order = GhostType order
            instance.GetComponent<AI_ModelVisual>()?.Apply((int)pending.Possession.GhostType);
            _factory.SpawnHandler.Spawn(instance);
            _entities.Add(entity);
            _activeEntityCount = _entities.Count;
        }

        // A free NavMesh point around the host, clear of villagers and walls; the home point when none is found
        private static Vector3 PickSpawnPoint(Vector3 host, Vector3 fallback)
        {
            for (int i = 0; i < SPAWN_TRIES; i++)
            {
                float angle = i * Mathf.PI * 2f / SPAWN_TRIES;
                Vector3 wanted = host + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SPAWN_BESIDE_HOST;
                if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, SPAWN_BESIDE_HOST, NavMesh.AllAreas)
                    || NavMesh.Raycast(host, hit.position, out _, NavMesh.AllAreas) || IsCrowded(hit.position))
                {
                    continue;
                }

                return hit.position;
            }

            return fallback;
        }

        private static bool IsCrowded(Vector3 point)
        {
            foreach (Collider other in Physics.OverlapSphere(point + Vector3.up, SPAWN_CLEARANCE, ~0, QueryTriggerInteraction.Ignore))
            {
                if (other.GetComponentInParent<AI_Core>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void HandleDayStart()
        {
            // A host must always reach home: one that never slept there is a bug to fix, not a case to cover (C121)
            foreach (PendingEntity pending in _pendingEntities)
            {
                AILog.Error(AILog.GHOST, $"[{pending.Possession.GhostId}] {pending.Host.LogId} never fell asleep at home, no ghost came out last night");
            }

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
