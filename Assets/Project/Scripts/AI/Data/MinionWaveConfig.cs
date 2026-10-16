using System.Collections.Generic;
using UnityEngine;

namespace ProjectShaman.AI.Data
{
    [CreateAssetMenu(fileName = "MinionWaveConfig", menuName = "ProjectShaman/AI/Minion Wave Config")]
    public class MinionWaveConfig : ScriptableObject
    {
        [Header("Wave")]
        [SerializeField, Min(0f)] private float _firstWaveDelay = 2f;
        [SerializeField, Min(0.5f)] private float _waveIntervalSeconds = 15f;
        [SerializeField, Min(1)] private int _minionsPerWave = 4;
        [SerializeField, Min(0)] private int _countGrowthPerWave;
        [SerializeField, Min(0)] private int _maxWaveCount;
        [SerializeField, Min(0f)] private float _spawnStaggerSeconds = 0.4f;
        [SerializeField, Min(1)] private int _maxAliveMinions = 20;

        [Header("Spawn Position")]
        [SerializeField, Min(0f)] private float _spawnJitterRadius = 1.5f;
        [SerializeField, Min(0f)] private float _minPlayerDistance = 4f;
        [SerializeField, Min(1)] private int _spawnPositionAttempts = 6;

        [Header("Yin Scaling (extension)")]
        [SerializeField] private bool _useYinScaling;
        [SerializeField] private List<MinionWaveTier> _yinTiers = new List<MinionWaveTier>
        {
            new MinionWaveTier(0f, 1f, 0),
            new MinionWaveTier(50f, 0.85f, 1),
            new MinionWaveTier(80f, 0.7f, 2)
        };

        public float FirstWaveDelay => _firstWaveDelay;
        public float WaveIntervalSeconds => Mathf.Max(0.5f, _waveIntervalSeconds);
        public int MinionsPerWave => Mathf.Max(1, _minionsPerWave);
        public int CountGrowthPerWave => Mathf.Max(0, _countGrowthPerWave);
        public int MaxWaveCount => Mathf.Max(0, _maxWaveCount);
        public float SpawnStaggerSeconds => _spawnStaggerSeconds;
        public int MaxAliveMinions => Mathf.Max(1, _maxAliveMinions);
        public float SpawnJitterRadius => _spawnJitterRadius;
        public float MinPlayerDistance => _minPlayerDistance;
        public int SpawnPositionAttempts => Mathf.Max(1, _spawnPositionAttempts);
        public bool UseYinScaling => _useYinScaling;
        public IReadOnlyList<MinionWaveTier> YinTiers => _yinTiers;

        public bool TryGetYinTier(float yin, out MinionWaveTier tier)
        {
            tier = default;
            bool found = false;

            if (_yinTiers == null)
            {
                return false;
            }

            foreach (MinionWaveTier candidate in _yinTiers)
            {
                if (yin >= candidate.MinYin && (!found || candidate.MinYin > tier.MinYin))
                {
                    tier = candidate;
                    found = true;
                }
            }

            return found;
        }
    }
}
