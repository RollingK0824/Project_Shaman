using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ProjectShaman.AI.Core;
using ProjectShaman.AI.Data;
using ProjectShaman.AI.Defense;

namespace ProjectShaman.AI.Minion
{
    public class MinionSpawner : MonoBehaviour
    {
        private const float NAVMESH_SAMPLE_DISTANCE = 2f;

        [SerializeField] private AIFactory _factory;
        [SerializeField] private DefenseVoteResolver _voteResolver;
        [SerializeField] private GameObject _minionPrefab;
        [SerializeField] private MinionWaveConfig _config;
        [SerializeField] private Transform _objective;
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField, Min(1f)] private float _fallbackSpawnRadius = 15f;
        [Tooltip("Flying minions appear in the air at the spawn point's own height, scattered up and down by this much")]
        [SerializeField, Min(0f)] private float _airSpawnHeightJitter = 1.5f;

        [Header("Runtime (server only)")]
        [SerializeField] private bool _isRunning;
        [SerializeField] private string _settingsInfo;
        [SerializeField] private int _waveIndex;
        [SerializeField] private int _pendingSpawns;
        [SerializeField] private int _aliveCount;
        [SerializeField] private int _spawnedTotal;
        [SerializeField] private float _nextWaveIn;

        private readonly List<AI_Minion> _alive = new List<AI_Minion>();
        private DefenseManager _defense;
        private TimeManager _time;
        private MinionBattlefield _battlefield;
        private MinionWaveSettings _settings;
        private System.Random _random;
        private float _nextWaveTime;
        private float _nextSpawnTime;

        public bool IsRunning => _isRunning;
        public int AliveCount => _alive.Count;

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

            Unsubscribe();
        }

        private void HandleVillagersCreated(IReadOnlyList<AI_Core> cores)
        {
            Unsubscribe();

            if (_minionPrefab == null || _config == null || _objective == null)
            {
                AILog.Error(AILog.MINION, "MinionSpawner missing references (prefab, wave config or objective)");
                return;
            }

            _defense = DefenseManager.Existing != null ? DefenseManager.Existing : FindFirstObjectByType<DefenseManager>();
            if (_defense == null)
            {
                AILog.Warn(AILog.MINION, "DefenseManager not in scene, minions spawn only through BeginDefense");
            }
            else
            {
                _defense.DefenseStarted += HandleDefenseStarted;
                _defense.DefenseCompleted += HandleDefenseCompleted;
            }

            _time = TimeManager.Existing;
            if (_time != null)
            {
                _time.OnDayStart += HandleDayStart;
            }
        }

        private void HandleVillagersCleared()
        {
            StopDefense("factory cleared");
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_defense != null)
            {
                _defense.DefenseStarted -= HandleDefenseStarted;
                _defense.DefenseCompleted -= HandleDefenseCompleted;
                _defense = null;
            }

            if (_time != null)
            {
                _time.OnDayStart -= HandleDayStart;
                _time = null;
            }
        }

        private void HandleDefenseStarted()
        {
            BeginDefense(ResolveYin());
        }

        private void HandleDefenseCompleted(bool won)
        {
            StopDefense(won ? "defense won" : "defense lost");
        }

        private void HandleDayStart()
        {
            StopDefense("morning");
        }

        private float ResolveYin()
        {
            int day = TimeManager.Existing != null ? TimeManager.Existing.DayCount : 0;

            if (_voteResolver != null && _voteResolver.HasCorrectVerdict && _voteResolver.LastVerdict.Day == day)
            {
                return _voteResolver.LastVerdict.Yin;
            }

            AILog.Warn(AILog.MINION, $"No correct verdict for Day{day}, waves use yin 0");
            return 0f;
        }

        public void BeginDefense(float yin)
        {
            if (_factory == null || _factory.SpawnHandler == null || !_factory.SpawnHandler.CanSpawn)
            {
                AILog.Error(AILog.MINION, "Spawn handler not ready, defense waves skipped");
                return;
            }

            StopDefense("restart");

            _settings = MinionWavePlanner.Resolve(_config, yin);
            _settingsInfo = _settings.ToString();
            IDamageable objectiveDamageable = _objective.GetComponentInParent<IDamageable>();
            _battlefield = new MinionBattlefield(_objective, objectiveDamageable, _factory.SpawnHandler);
            _battlefield.RefreshPlayers();
            _random = new System.Random(unchecked(_factory.Seed * 7919 + (TimeManager.Existing != null ? TimeManager.Existing.DayCount : 0)));
            _waveIndex = 0;
            _pendingSpawns = 0;
            _nextWaveTime = Time.time + _settings.FirstWaveDelay;
            _nextSpawnTime = 0f;
            _isRunning = true;

            if (objectiveDamageable == null)
            {
                AILog.Log(AILog.STUB, $"Objective {_objective.name} has no IDamageable (flag is player part), contact will only despawn minions");
            }

            AILog.Log(AILog.MINION, $"Defense waves begin ({_settingsInfo})");
        }

        public void StopDefense(string reason)
        {
            bool hadMinions = _alive.Count > 0;

            if (!_isRunning && !hadMinions)
            {
                return;
            }

            _isRunning = false;
            _pendingSpawns = 0;
            _nextWaveIn = 0f;

            List<AI_Minion> remaining = new List<AI_Minion>(_alive);
            foreach (AI_Minion minion in remaining)
            {
                if (minion != null)
                {
                    minion.Remove(reason);
                }
            }

            _alive.Clear();
            _aliveCount = 0;
            AILog.Log(AILog.MINION, $"Defense waves stopped ({reason}, waves {_waveIndex}, spawned {_spawnedTotal}, cleared {remaining.Count})");
        }

        private void Update()
        {
            if (!_isRunning)
            {
                return;
            }

            _battlefield.RefreshPlayersIfDue();

            if (Time.time >= _nextWaveTime && _settings.HasWave(_waveIndex))
            {
                int count = _settings.CountForWave(_waveIndex);
                _pendingSpawns += count;
                AILog.Log(AILog.MINION, $"Wave {_waveIndex + 1} (+{count}, pending {_pendingSpawns}, alive {_alive.Count})");
                _waveIndex++;
                _nextWaveTime += _settings.WaveInterval;
            }

            _nextWaveIn = _settings.HasWave(_waveIndex) ? Mathf.Max(0f, _nextWaveTime - Time.time) : -1f;

            if (_pendingSpawns > 0 && Time.time >= _nextSpawnTime && _alive.Count < _settings.MaxAlive)
            {
                if (SpawnMinion())
                {
                    _pendingSpawns--;
                }

                _nextSpawnTime = Time.time + _settings.SpawnStagger;
            }
        }

        private bool SpawnMinion()
        {
            if (!TryPickSpawnPosition(out Vector3 position))
            {
                AILog.Warn(AILog.MINION, "No spawn position on NavMesh, retrying");
                return false;
            }

            Vector3 facing = _objective.position - position;
            facing.y = 0f;
            Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;

            GameObject instance = Instantiate(_minionPrefab, position, rotation);
            _spawnedTotal++;
            instance.name = $"Minion_{_spawnedTotal:000}";

            AI_Minion minion = instance.GetComponent<AI_Minion>();
            if (minion == null)
            {
                AILog.Error(AILog.MINION, $"{_minionPrefab.name} has no AI_Minion");
                Destroy(instance);
                return false;
            }

            minion.Initialize(_battlefield);

            AI_DeathDespawner despawner = instance.GetComponent<AI_DeathDespawner>();
            if (despawner != null)
            {
                despawner.SetSpawnHandler(_factory.SpawnHandler);
            }
            else
            {
                AILog.Warn(AILog.MINION, $"{_minionPrefab.name} has no AI_DeathDespawner, killed minions will not despawn");
            }

            minion.OnRemoved += HandleMinionRemoved;
            _alive.Add(minion);
            _aliveCount = _alive.Count;
            _factory.SpawnHandler.Spawn(instance);
            return true;
        }

        private void HandleMinionRemoved(AI_Minion minion)
        {
            minion.OnRemoved -= HandleMinionRemoved;
            _alive.Remove(minion);
            _aliveCount = _alive.Count;
        }

        private bool IsFlyingMinion => _minionPrefab != null && _minionPrefab.GetComponent<AI_FlyingMovement>() != null;

        private bool TryPickSpawnPosition(out Vector3 position)
        {
            bool hasFallback = false;
            position = Vector3.zero;

            for (int i = 0; i < _config.SpawnPositionAttempts; i++)
            {
                Vector3 candidate = PickSpawnAnchor();
                float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
                float distance = Mathf.Sqrt((float)_random.NextDouble()) * _config.SpawnJitterRadius;
                candidate += new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                // Flyers keep the spawn point's height (set in the scene) instead of being put down on the NavMesh
                if (IsFlyingMinion)
                {
                    candidate.y += ((float)_random.NextDouble() * 2f - 1f) * _airSpawnHeightJitter;
                }
                else if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_DISTANCE, NavMesh.AllAreas))
                {
                    candidate = hit.position;
                }
                else
                {
                    continue;
                }

                if (!hasFallback)
                {
                    position = candidate;
                    hasFallback = true;
                }

                if (!_battlefield.IsNearAnyPlayer(candidate, _config.MinPlayerDistance))
                {
                    position = candidate;
                    return true;
                }
            }

            return hasFallback;
        }

        private Vector3 PickSpawnAnchor()
        {
            int validCount = 0;

            if (_spawnPoints != null)
            {
                foreach (Transform point in _spawnPoints)
                {
                    if (point != null)
                    {
                        validCount++;
                    }
                }
            }

            if (validCount > 0)
            {
                int pick = _random.Next(validCount);

                foreach (Transform point in _spawnPoints)
                {
                    if (point == null)
                    {
                        continue;
                    }

                    if (pick == 0)
                    {
                        return point.position;
                    }

                    pick--;
                }
            }

            float angle = (float)(_random.NextDouble() * Mathf.PI * 2f);
            return _objective.position + new Vector3(Mathf.Cos(angle) * _fallbackSpawnRadius, 0f, Mathf.Sin(angle) * _fallbackSpawnRadius);
        }

        private void OnDrawGizmosSelected()
        {
            if (_objective == null)
            {
                return;
            }

            Gizmos.color = new Color(0.9f, 0.3f, 0.2f);
            Gizmos.DrawWireSphere(_objective.position, 1f);

            if (_spawnPoints == null || _spawnPoints.Length == 0)
            {
                Gizmos.DrawWireSphere(_objective.position, _fallbackSpawnRadius);
                return;
            }

            foreach (Transform point in _spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawLine(point.position, _objective.position);
                }
            }
        }
    }
}
