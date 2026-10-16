using System;

namespace ProjectShaman.AI.Data
{
    [Serializable]
    public struct HuntTier
    {
        public float MinYin;
        public int Kills;
        public bool ChasePlayer;

        public HuntTier(float minYin, int kills, bool chasePlayer)
        {
            MinYin = minYin;
            Kills = kills;
            ChasePlayer = chasePlayer;
        }

        public override string ToString()
        {
            return $"yin≥{MinYin:F0} kills={Kills}{(ChasePlayer ? " +chase" : string.Empty)}";
        }
    }
}
