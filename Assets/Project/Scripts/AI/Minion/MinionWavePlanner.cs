using UnityEngine;
using ProjectShaman.AI.Data;

namespace ProjectShaman.AI.Minion
{
    public readonly struct MinionWaveSettings
    {
        public readonly float FirstWaveDelay;
        public readonly float WaveInterval;
        public readonly int BaseCount;
        public readonly int CountGrowthPerWave;
        public readonly int MaxWaveCount;
        public readonly float SpawnStagger;
        public readonly int MaxAlive;
        public readonly float Yin;

        public MinionWaveSettings(float firstWaveDelay, float waveInterval, int baseCount, int countGrowthPerWave, int maxWaveCount, float spawnStagger, int maxAlive, float yin)
        {
            FirstWaveDelay = firstWaveDelay;
            WaveInterval = waveInterval;
            BaseCount = baseCount;
            CountGrowthPerWave = countGrowthPerWave;
            MaxWaveCount = maxWaveCount;
            SpawnStagger = spawnStagger;
            MaxAlive = maxAlive;
            Yin = yin;
        }

        public bool HasWave(int waveIndex)
        {
            return MaxWaveCount <= 0 || waveIndex < MaxWaveCount;
        }

        public int CountForWave(int waveIndex)
        {
            return Mathf.Max(1, BaseCount + CountGrowthPerWave * Mathf.Max(0, waveIndex));
        }

        public override string ToString()
        {
            string waves = MaxWaveCount > 0 ? MaxWaveCount.ToString() : "∞";
            return $"yin {Yin:F0}, first {FirstWaveDelay:F1}s, interval {WaveInterval:F1}s, count {BaseCount}+{CountGrowthPerWave}/wave, waves {waves}, maxAlive {MaxAlive}";
        }
    }

    public static class MinionWavePlanner
    {
        public static MinionWaveSettings Resolve(MinionWaveConfig config, float yin)
        {
            float interval = config.WaveIntervalSeconds;
            int count = config.MinionsPerWave;

            if (config.UseYinScaling && config.TryGetYinTier(yin, out MinionWaveTier tier))
            {
                interval = Mathf.Max(0.5f, interval * Mathf.Max(0.01f, tier.IntervalMultiplier));
                count = Mathf.Max(1, count + tier.CountBonus);
            }

            return new MinionWaveSettings(config.FirstWaveDelay, interval, count, config.CountGrowthPerWave, config.MaxWaveCount, config.SpawnStaggerSeconds, config.MaxAliveMinions, yin);
        }
    }
}
