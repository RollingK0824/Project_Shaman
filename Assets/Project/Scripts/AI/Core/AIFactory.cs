using System;
using System.Collections.Generic;
using UnityEngine;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defines;
using ProjectShaman.AI.Ghost;
using ProjectShaman.AI.Interfaces;
using ProjectShaman.AI.Mock;
using ProjectShaman.AI.Routine;
using ProjectShaman.AI.World;

namespace ProjectShaman.AI.Core
{
    public class AIFactory : MonoBehaviour
    {
        private const int MAX_ROLL_ATTEMPTS = 20;

        [SerializeField] private GameObject _villagerPrefab;
        [SerializeField] private ScriptableObject _dataProviderAsset;
        [SerializeField] private AIBehaviourConfig _behaviourConfig;
        [SerializeField] private MonoBehaviour _timeSourceBehaviour;
        [SerializeField] private int _villagerCount = 1;
        [SerializeField] private int _ghostCount = 1;
        [SerializeField] private bool _useFixedSeed = true;
        [SerializeField] private int _randomSeed = 1234;
        [SerializeField] private Transform _spawnRoot;
        [SerializeField] private Transform[] _testHomePoints;

        private readonly List<AI_Core> _spawnedVillagers = new List<AI_Core>();
        private readonly HashSet<string> _usedNames = new HashSet<string>();
        private IAIDataProvider _dataProvider;
        private ITimeSource _timeSource;
        private ScheduleBuilder _scheduleBuilder;
        private System.Random _random;
        private bool _hasCreated;

        public GameObject VillagerPrefab => _villagerPrefab;
        public IAIDataProvider DataProvider => _dataProvider;
        public ISpawnHandler SpawnHandler { get; private set; }
        public IReadOnlyList<AI_Core> SpawnedVillagers => _spawnedVillagers;
        public int Seed { get; private set; }

        public event System.Action<IReadOnlyList<AI_Core>> OnVillagersCreated;
        public event System.Action OnVillagersCleared;

        public void CreateVillagers(ISpawnHandler spawnHandler)
        {
            if (_hasCreated)
            {
                AILog.Warn(AILog.FACTORY, "CreateVillagers already called, ignored");
                return;
            }

            if (spawnHandler == null || !spawnHandler.CanSpawn)
            {
                AILog.Error(AILog.FACTORY, "SpawnHandler missing or not ready");
                return;
            }

            if (_villagerPrefab == null)
            {
                AILog.Error(AILog.FACTORY, "Villager prefab not assigned");
                return;
            }

            _dataProvider = _dataProviderAsset as IAIDataProvider;
            if (_dataProvider == null)
            {
                AILog.Error(AILog.FACTORY, "Data provider asset missing or does not implement IAIDataProvider");
                return;
            }

            _timeSource = _timeSourceBehaviour as ITimeSource;
            if (_timeSource == null || _behaviourConfig == null)
            {
                AILog.Error(AILog.FACTORY, "Time source or behaviour config missing");
                return;
            }

            _hasCreated = true;
            SpawnHandler = spawnHandler;
            Seed = _useFixedSeed ? _randomSeed : Environment.TickCount;
            _random = new System.Random(Seed);

            AILog.Log(AILog.FACTORY, $"Spawn begin (count={_villagerCount}, seed={Seed}, handler={spawnHandler.HandlerName})");
            AILog.Log(AILog.DATA_TO_AI, $"{_dataProvider.SourceName} loaded (names={_dataProvider.Names.Count}, jobs={_dataProvider.Jobs.Count}, routines={_dataProvider.Routines.Count})");

            _scheduleBuilder = new ScheduleBuilder(_dataProvider.Routines, _behaviourConfig.CycleDays, _timeSource.SlotsPerDay, _behaviourConfig.MinRestPerDay, _random, GetPlaceCapacity);

            List<VillagerProfile> profiles = GenerateProfiles();

            AssignHouses(profiles);
            AssignGhosts(profiles);

            foreach (VillagerProfile profile in profiles)
            {
                AI_Core core = InstantiateVillager(profile);
                if (core == null)
                {
                    continue;
                }

                AttachPossession(core, profile);
                core.Initialize(profile);

                AILog.Log(AILog.FACTORY_TO_NET, profile.VillagerId, $"Spawn request via {spawnHandler.HandlerName}");
                spawnHandler.Spawn(core.gameObject);

                _spawnedVillagers.Add(core);
            }

            AILog.Log(AILog.FACTORY, $"Spawn end (spawned={_spawnedVillagers.Count})");
            if (OnVillagersCreated == null)
            {
                AILog.Warn(AILog.FACTORY, "No listener for OnVillagersCreated (AIManager missing?), routines will not run");
            }

            OnVillagersCreated?.Invoke(_spawnedVillagers);
        }

        public void ResetFactory()
        {
            if (!_hasCreated)
            {
                return;
            }

            AILog.Log(AILog.FACTORY, $"Reset (cleared {_spawnedVillagers.Count} villagers)");
            OnVillagersCleared?.Invoke();

            _spawnedVillagers.Clear();
            _usedNames.Clear();
            _scheduleBuilder = null;
            _hasCreated = false;
        }

        private int GetPlaceCapacity(string placeId)
        {
            if (PlaceRegistry.TryGet(placeId, out PlaceArea place))
            {
                return place.Capacity;
            }

            AILog.Warn(AILog.ROUTINE, $"Place '{placeId}' not registered, routines using it are excluded");
            return 0;
        }

        private List<VillagerProfile> GenerateProfiles()
        {
            List<VillagerProfile> profiles = new List<VillagerProfile>();

            for (int i = 0; i < _villagerCount; i++)
            {
                VillagerProfile profile = new VillagerProfile(i);

                if (!TryRollVillager(profile))
                {
                    AILog.Error(AILog.FACTORY, $"{profile.VillagerId} failed to roll valid combination after {MAX_ROLL_ATTEMPTS} attempts, skipped");
                    continue;
                }

                profile.PublicInfo.DisplayName = PickName(profile.PublicInfo);

                AILog.Log(AILog.FACTORY, profile.VillagerId, $"Profile generated {profile.PublicInfo}");
                AILog.Log(AILog.ROUTINE, profile.VillagerId, $"Schedule {profile.Schedule}");
                profiles.Add(profile);
            }

            return profiles;
        }

        private bool TryRollVillager(VillagerProfile profile)
        {
            VillagerPublicInfo info = profile.PublicInfo;

            for (int attempt = 1; attempt <= MAX_ROLL_ATTEMPTS; attempt++)
            {
                info.Gender = RollEnum<VillagerGender>();
                info.SocialClass = RollEnum<VillagerSocialClass>();
                info.AgeGroup = RollEnum<VillagerAgeGroup>();

                JobEntry job = PickJob(info);
                if (job == null)
                {
                    AILog.Log(AILog.FACTORY, info.VillagerId, $"Reroll #{attempt}: no job for {info.Gender}/{info.SocialClass}/{info.AgeGroup}");
                    continue;
                }

                info.JobId = job.JobId;

                if (!_scheduleBuilder.TryBuild(info, out VillagerSchedule schedule))
                {
                    AILog.Log(AILog.FACTORY, info.VillagerId, $"Reroll #{attempt}: schedule build failed for {info.JobId}");
                    continue;
                }

                profile.Schedule = schedule;
                return true;
            }

            return false;
        }

        private JobEntry PickJob(VillagerPublicInfo info)
        {
            List<JobEntry> candidates = new List<JobEntry>();

            foreach (JobEntry job in _dataProvider.Jobs)
            {
                if (job != null && !string.IsNullOrEmpty(job.JobId) && job.Condition.Matches(info.Gender, info.SocialClass, info.AgeGroup))
                {
                    candidates.Add(job);
                }
            }

            return candidates.Count > 0 ? candidates[_random.Next(candidates.Count)] : null;
        }

        private string PickName(VillagerPublicInfo info)
        {
            List<string> available = new List<string>();

            foreach (NameEntry entry in _dataProvider.Names)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Name) || _usedNames.Contains(entry.Name))
                {
                    continue;
                }

                if (entry.Condition.Matches(info.Gender, info.SocialClass, info.AgeGroup))
                {
                    available.Add(entry.Name);
                }
            }

            if (available.Count == 0)
            {
                AILog.Warn(AILog.DATA_TO_AI, $"{info.VillagerId} no unused name for {info.Gender}/{info.SocialClass}/{info.AgeGroup}, fallback to id");
                return info.VillagerId;
            }

            string picked = available[_random.Next(available.Count)];
            _usedNames.Add(picked);
            AILog.Log(AILog.DATA_TO_AI, info.VillagerId, $"Name picked '{picked}' ({available.Count} available)");
            return picked;
        }

        private void AssignHouses(List<VillagerProfile> profiles)
        {
            foreach (VillagerProfile profile in profiles)
            {
                profile.HomePosition = GetTestPoint(_testHomePoints, profile.Index);
            }

            AILog.Log(AILog.STUB, $"AssignHouses using test home points ({profiles.Count})");
        }

        private void AssignGhosts(List<VillagerProfile> profiles)
        {
            List<VillagerProfile> pool = new List<VillagerProfile>(profiles);
            int count = Mathf.Min(_ghostCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                int index = _random.Next(pool.Count);
                VillagerProfile host = pool[index];
                pool.RemoveAt(index);

                host.IsPossessed = true;
                host.GhostId = $"GHOST_{i:00}";
                AILog.Log(AILog.GHOST, host.VillagerId, $"Assigned as host of {host.GhostId}");
            }

            AILog.Log(AILog.FACTORY, $"AssignGhosts {count}/{profiles.Count}");
        }

        private void AttachPossession(AI_Core core, VillagerProfile profile)
        {
            if (!profile.IsPossessed)
            {
                return;
            }

            AI_Possession possession = core.gameObject.AddComponent<AI_Possession>();
            possession.Initialize(profile.GhostId, _random.Next(), _behaviourConfig.InitialYin);
            profile.Possession = possession;
        }

        private Vector3 GetTestPoint(Transform[] points, int index)
        {
            if (points == null || points.Length == 0)
            {
                return transform.position;
            }

            Transform point = points[index % points.Length];
            return point != null ? point.position : transform.position;
        }

        private T RollEnum<T>() where T : Enum
        {
            Array values = Enum.GetValues(typeof(T));
            return (T)values.GetValue(_random.Next(values.Length));
        }

        private AI_Core InstantiateVillager(VillagerProfile profile)
        {
            GameObject instance = Instantiate(_villagerPrefab, profile.HomePosition, Quaternion.identity, _spawnRoot);
            instance.name = $"Villager_{profile.VillagerId}";

            AI_Core core = instance.GetComponent<AI_Core>();
            if (core == null)
            {
                AILog.Error(AILog.FACTORY, $"{instance.name} has no AI_Core");
                Destroy(instance);
                return null;
            }

            AILog.Log(AILog.FACTORY, profile.VillagerId, $"Instantiated at {profile.HomePosition}");
            return core;
        }

        [ContextMenu("Create Villagers Offline")]
        private void CreateVillagersOffline()
        {
            if (!Application.isPlaying)
            {
                AILog.Warn(AILog.FACTORY, "Offline create works only in Play Mode");
                return;
            }

            CreateVillagers(new LocalSpawnHandler());
        }

        private void OnValidate()
        {
            if (_dataProviderAsset != null && !(_dataProviderAsset is IAIDataProvider))
            {
                Debug.LogWarning($"[AI][{AILog.FACTORY}] {_dataProviderAsset.name} does not implement IAIDataProvider");
                _dataProviderAsset = null;
            }

            if (_timeSourceBehaviour != null && !(_timeSourceBehaviour is ITimeSource))
            {
                Debug.LogWarning($"[AI][{AILog.FACTORY}] {_timeSourceBehaviour.name} does not implement ITimeSource");
                _timeSourceBehaviour = null;
            }

            if (_villagerCount < 0)
            {
                _villagerCount = 0;
            }
        }
    }
}
