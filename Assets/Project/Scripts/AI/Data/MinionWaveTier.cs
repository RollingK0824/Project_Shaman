using System;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public struct MinionWaveTier
    {
        public float MinYin;
        public float IntervalMultiplier;
        public int CountBonus;

        public MinionWaveTier(float minYin, float intervalMultiplier, int countBonus)
        {
            MinYin = minYin;
            IntervalMultiplier = intervalMultiplier;
            CountBonus = countBonus;
        }

        public override string ToString()
        {
            return $"yin≥{MinYin:F0} interval×{IntervalMultiplier:F2} count+{CountBonus}";
        }
    }
}
